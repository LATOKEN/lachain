using RocksDbSharp;
using System;
using Lachain.Utility.Utils;
using Lachain.Storage.DbCompact;

namespace Lachain.Storage.Trie
{
    internal class NodeRepository
    {
        private readonly IRocksDbContext _rocksDbContext;

        public NodeRepository(IRocksDbContext rocksDbContext)
        {
            _rocksDbContext = rocksDbContext;
        }

        public IHashTrieNode? GetNode(ulong id)
        {
            if(id==0) Console.WriteLine("0000000000000") ;
            var prefix = EntryPrefix.PersistentHashMap.BuildPrefix(id);
            var raw = _rocksDbContext.Get(prefix);
            if (raw is null) return null;
            return NodeSerializer.FromBytes(raw);
        }

        public WriteBatch CreateBatch()
        {
            return new WriteBatch();
        }

        public void DeleteNodeToBatch(ulong id, RocksDbAtomicWrite tx)
        {
            var prefix = EntryPrefix.PersistentHashMap.BuildPrefix(id);
            tx.Delete(prefix);
        }

        public void WriteNodeToBatch(ulong id, IHashTrieNode node, RocksDbAtomicWrite tx)
        {
            var prefix = EntryPrefix.PersistentHashMap.BuildPrefix(id);
            tx.Put(prefix, NodeSerializer.ToBytes(node));
            var hashPrefix = EntryPrefix.VersionByHash.BuildPrefix(node.Hash);
            tx.Put(hashPrefix, UInt64Utils.ToBytes(id));
        }

        public void SaveBatch(WriteBatch batch)
        {
            _rocksDbContext.SaveBatch(batch);
        }

        public bool NodeIdExist(ulong id, IDbShrinkRepository _repo)
        {
            var prefix = EntryPrefix.NodeIdForRecentSnapshot.BuildPrefix(id);
            return _repo.KeyExists(prefix);
        }

        public void WriteNodeId(ulong id, IDbShrinkRepository _repo)
        {
            // saving nodes that are reachable from recent snapshots temporarily
            // so that all other nodes can be deleted. 
            var prefix = EntryPrefix.NodeIdForRecentSnapshot.BuildPrefix(id);
            _repo.Save(prefix, new byte[1]);
        }

        public void DeleteNodeId(ulong id, IDbShrinkRepository _repo)
        {
            // Deleting temporary nodes
            var prefix = EntryPrefix.NodeIdForRecentSnapshot.BuildPrefix(id);
            _repo.Delete(prefix);
        }

        public void DeleteNode(ulong id , IHashTrieNode node, IDbShrinkRepository _repo)
        {
            // first delete the version by hash and then delete the node.
            var prefix = EntryPrefix.VersionByHash.BuildPrefix(node.Hash);
            _repo.Delete(prefix, false);
            prefix = EntryPrefix.PersistentHashMap.BuildPrefix(id);
            _repo.Delete(prefix);
        }
    }
}