using System.Linq;
using System.Collections.Concurrent;
using Lachain.Proto;
using Lachain.Crypto;
using Lachain.Storage.Trie;
using Lachain.Utility.Serialization;
using Lachain.Utility.Utils;

namespace Lachain.Storage.DbCompact
{
    public class DbShrinkRepository : IDbShrinkRepository
    {
        private IRocksDbContext _dbContext;
        private RocksDbAtomicWrite batch;
        private readonly ConcurrentDictionary<UInt256, byte[]?> _memDb = new ConcurrentDictionary<UInt256, byte[]?>();

        public DbShrinkRepository(IRocksDbContext dbContext)
        {
            _dbContext = dbContext;
            UpdateBatch();
        }

        private void UpdateBatch()
        {
            batch = new RocksDbAtomicWrite(_dbContext);
            _memDb.Clear();
        }

        private void Save(byte[] key, byte[] content, bool tryCommit = true)
        {
            batch.Put(key, content);
            var keyHash = key.Keccak();
            _memDb[keyHash] = content;
            DbShrinkUtils.UpdateCounter();
            if(tryCommit && DbShrinkUtils.CycleEnded())
            {
                Commit();
            }
        }

        private void Delete(byte[] key, bool tryCommit = true)
        {
            batch.Delete(key);
            var keyHash = key.Keccak();
            _memDb[keyHash] = null;
            DbShrinkUtils.UpdateCounter();
            if(tryCommit && DbShrinkUtils.CycleEnded())
            {
                Commit();
            }
        }

        private void Commit()
        {
            batch.Commit();
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

        public void WriteNodeId(ulong id)
        {
            // saving nodes that are reachable from recent snapshots temporarily
            // so that all other nodes can be deleted. 
            var prefix = EntryPrefix.NodeIdForRecentSnapshot.BuildPrefix(id);
            Save(prefix, new byte[1]);
        }

        public void DeleteNodeId(ulong id)
        {
            // Deleting temporary nodes
            var prefix = EntryPrefix.NodeIdForRecentSnapshot.BuildPrefix(id);
            Delete(prefix);
        }

        public IHashTrieNode? GetNodeById(ulong id)
        {
            var prefix = EntryPrefix.PersistentHashMap.BuildPrefix(id);
            var content = Get(prefix);
            if (content is null) return null;
            return NodeSerializer.FromBytes(content);
        }
        public void DeleteNode(ulong id , IHashTrieNode node)
        {
            // first delete the version by hash and then delete the node.
            var prefix = EntryPrefix.VersionByHash.BuildPrefix(node.Hash);
            Delete(prefix, false);
            prefix = EntryPrefix.PersistentHashMap.BuildPrefix(id);
            Delete(prefix);
        }

        public void DeleteVersion(uint repository, ulong block, ulong version)
        {
            // this method is used to delete old snapshot. Don't use it for other purpose.
            // we need to delete all seven snapshots. If some are deleted and some are not then
            // later we cannot access non deleted snapshots using available methods.
            // so set the third optional parameter as false
            Delete(
                EntryPrefix.SnapshotIndex.BuildPrefix(
                    repository.ToBytes().Concat(block.ToBytes()).ToArray()),
                false
            );
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

        public void DeleteStatusAndDepth()
        {
            var prefix = EntryPrefix.DbShrinkStatus.BuildPrefix();
            Delete(prefix, false);
            prefix = EntryPrefix.DbShrinkDepth.BuildPrefix();
            Delete(prefix, false);
            Commit();
        }

    }
}