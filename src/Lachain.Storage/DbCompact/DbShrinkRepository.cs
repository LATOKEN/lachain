using System;
using System.Linq;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Lachain.Proto;
using Lachain.Crypto;
using Lachain.Storage.Trie;
using Lachain.Utility.Serialization;
using Lachain.Utility.Utils;
using RocksDbSharp;

namespace Lachain.Storage.DbCompact
{
    public class DbShrinkRepository : IDbShrinkRepository
    {
        private IRocksDbContext _dbContext;
        private RocksDbAtomicWrite? batch;
        private readonly ConcurrentDictionary<UInt256, byte[]?> _memDb = new ConcurrentDictionary<UInt256, byte[]?>();

        public DbShrinkRepository(IRocksDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Initialize()
        {
            if (batch is null)
                batch = new RocksDbAtomicWrite(_dbContext);
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        private void UpdateBatch()
        {
            batch = new RocksDbAtomicWrite(_dbContext);
            _memDb.Clear();
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        private void Save(byte[] key, byte[] content, bool tryCommit = true)
        {
            Initialize();
            batch!.Put(key, content);
            var keyHash = key.Keccak();
            _memDb[keyHash] = content;
            DbShrinkUtils.UpdateCounter();
            if(tryCommit && DbShrinkUtils.CycleEnded())
            {
                Commit();
            }
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Delete(byte[] key, bool tryCommit = true)
        {
            Initialize();
            batch!.Delete(key);
            var keyHash = key.Keccak();
            _memDb[keyHash] = null;
            DbShrinkUtils.UpdateCounter();
            if(tryCommit && DbShrinkUtils.CycleEnded())
            {
                Commit();
            }
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        private void Commit()
        {
            Initialize();
            batch!.Commit();
            DbShrinkUtils.ResetCounter();
            UpdateBatch();
        }

        private byte[]? Get(byte[] key)
        {
            var keyHash = key.Keccak();
            if (_memDb.TryGetValue(keyHash, out var content))
            {
                return content;
            }
            return _dbContext.Get(key);
        }

        private bool KeyExists(byte[] key)
        {
            var keyHash = key.Keccak();
            if (_memDb.TryGetValue(keyHash, out var value))
            {
                if (value is null) return false;
                return true;
            }
            var content = _dbContext.Get(key);
            if (content is null) return false;
            return true;
        }

        public bool NodeIdExists(ulong id)
        {
            var prefix = EntryPrefix.NodeIdForRecentSnapshot.BuildPrefix(id);
            return KeyExists(prefix);
        }

        public void WriteNodeIdAndHash(ulong id, IHashTrieNode node)
        {
            // saving nodes that are reachable from recent snapshots temporarily
            // so that all other nodes can be deleted. 
            var prefix = EntryPrefix.NodeIdForRecentSnapshot.BuildPrefix(id);
            Save(prefix, new byte[1], false);
            prefix = EntryPrefix.NodeHashForRecentSnapshot.BuildPrefix(node.Hash);
            Save(prefix, new byte[1]);
        }

        public IHashTrieNode? GetNodeById(ulong id)
        {
            var prefix = EntryPrefix.PersistentHashMap.BuildPrefix(id);
            var content = Get(prefix);
            if (content is null) return null;
            return NodeSerializer.FromBytes(content);
        }

        public void DeleteVersion(uint repository, ulong block)
        {
            // we need to delete all seven snapshots. If some are deleted and some are not then
            // later we cannot access non deleted snapshots using available methods.
            // so set the third optional parameter as false
            Delete(
                EntryPrefix.SnapshotIndex.BuildPrefix(
                    repository.ToBytes().Concat(block.ToBytes()).ToArray()),
                false
            );
        }

        private void SetTimePassed(ulong timePassed)
        {
            var prefix = EntryPrefix.TimePassedMillis.BuildPrefix();
            Save(prefix, timePassed.ToBytes().ToArray());
        }

        private void SetSavedTime(ulong currentTime)
        {
            var prefix = EntryPrefix.LastSavedTimeMillis.BuildPrefix();
            Save(prefix, currentTime.ToBytes().ToArray());
        }

        public ulong TimePassed()
        {
            var prefix = EntryPrefix.TimePassedMillis.BuildPrefix();
            var raw = Get(prefix);
            if (raw is null) return 0;
            return BitConverter.ToUInt64(raw);
        }

        public ulong GetLastSavedTime()
        {
            var prefix = EntryPrefix.LastSavedTimeMillis.BuildPrefix();
            var raw = Get(prefix);
            if (raw is null) return 0;
            return BitConverter.ToUInt64(raw);
        }

        public void UpdateTime()
        {
            var currentTime = TimeUtils.CurrentTimeMillis();
            var timePassed = TimePassed();
            var lastTime = GetLastSavedTime();
            if (lastTime > 0)
                timePassed += currentTime - lastTime;
            SetTimePassed(timePassed);
            SetSavedTime(currentTime);
        }

        public void SetDbShrinkStatus(DbShrinkStatus status)
        {
            var prefix = EntryPrefix.DbShrinkStatus.BuildPrefix();
            var content = new byte[1];
            content[0] = (byte) status;
            Save(prefix, content, false);
            Commit();
        }

        public void SetDbShrinkDepth(ulong depth)
        {
            var prefix = EntryPrefix.DbShrinkDepth.BuildPrefix();
            Save(prefix, UInt64Utils.ToBytes(depth), false);
            Commit();
        }

        public ulong? GetDbShrinkDepth()
        {
            var prefix = EntryPrefix.DbShrinkDepth.BuildPrefix();
            var depth = Get(prefix);
            if (depth is null) return null;
            return UInt64Utils.FromBytes(depth);
        }

        public DbShrinkStatus GetDbShrinkStatus()
        {
            var prefix = EntryPrefix.DbShrinkStatus.BuildPrefix();
            var status = Get(prefix);
            if (status is null) 
            {
                return DbShrinkStatus.Stopped;
            }
            return (DbShrinkStatus) status[0];
        }

        public ulong GetOldestSnapshotInDb()
        {
            var prefix = EntryPrefix.OldestSnapshotInDb.BuildPrefix();
            var block = Get(prefix);
            if (block is null) return 0;
            return UInt64Utils.FromBytes(block);
        }

        public void SetOldestSnapshotInDb(ulong block)
        {
            var prefix = EntryPrefix.OldestSnapshotInDb.BuildPrefix();
            Save(prefix, UInt64Utils.ToBytes(block));
        }

        public void SaveTempKeyCount(ulong count)
        {
            var prefix = EntryPrefix.TotalTempKeysSaved.BuildPrefix();
            Save(prefix, UInt64Utils.ToBytes(count));
        }

        public ulong GetTempKeyCount()
        {
            var prefix = EntryPrefix.TotalTempKeysSaved.BuildPrefix();
            var raw = Get(prefix);
            return raw is null ? 0 : BitConverter.ToUInt64(raw);
        }

        public void SaveDeletedNodeCount(ulong count)
        {
            var prefix = EntryPrefix.TotalNodesDeleted.BuildPrefix();
            Save(prefix, UInt64Utils.ToBytes(count));
        }

        public ulong GetTotalNodesDeleted()
        {
            var prefix = EntryPrefix.TotalNodesDeleted.BuildPrefix();
            var raw = Get(prefix);
            return raw is null ? 0 : BitConverter.ToUInt64(raw);
        }

        public void DeleteAll()
        {
            var prefix = EntryPrefix.DbShrinkStatus.BuildPrefix();
            Delete(prefix, false);
            prefix = EntryPrefix.DbShrinkDepth.BuildPrefix();
            Delete(prefix, false);
            prefix = EntryPrefix.TimePassedMillis.BuildPrefix();
            Delete(prefix, false);
            prefix = EntryPrefix.LastSavedTimeMillis.BuildPrefix();
            Delete(prefix, false);
            prefix = EntryPrefix.TotalNodesDeleted.BuildPrefix();
            Delete(prefix, false);
            prefix = EntryPrefix.TotalTempKeysSaved.BuildPrefix();
            Delete(prefix, false);
            Commit();
        }

        public Iterator? GetIteratorForPrefixOnly(byte[] prefix)
        {
            var upperBound = new List<byte>(prefix).ToArray();
            bool lastPrefix = true;
            for (int iter = upperBound.Length - 1; iter >= 0; iter--)
            {
                if (upperBound[iter] < 255)
                {
                    upperBound[iter]++;
                    for (int j = iter + 1; j < upperBound.Length; j++)
                    {
                        upperBound[j] = 0;
                    }
                    lastPrefix = false;
                    break;
                }
            }
            if (lastPrefix) return _dbContext.GetIteratorForValidKeys(prefix);
            else return _dbContext.GetIteratorWithUpperBound(prefix, upperBound);
        }

    }
}