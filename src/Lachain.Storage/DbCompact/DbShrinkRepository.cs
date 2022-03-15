using System.Collections.Concurrent;
using System.Collections.Generic;
using Lachain.Proto;
using Lachain.Crypto;
using Lachain.Storage.Trie;

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

        public void Save(byte[] key, byte[] content, bool tryCommit = true)
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

        public void Delete(byte[] key, bool tryCommit = true)
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

        public void Commit()
        {
            batch.Commit();
            DbShrinkUtils.ResetCounter();
            UpdateBatch();
        }

        public byte[]? Get(byte[] key)
        {
            var keyHash = key.Keccak();
            if (_memDb.TryGetValue(keyHash, out var content))
            {
                return content;
            }
            return _dbContext.Get(key);
        }

        public bool KeyExists(byte[] key)
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

        public bool NodeIdExist(ulong id)
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

    }
}