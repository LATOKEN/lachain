using RocksDbSharp;
using System;
using Lachain.Utility.Utils;

namespace Lachain.Storage.Trie
{
    internal class NodeRepository
    {
        private readonly IRocksDbContext _rocksDbContext;

        public NodeRepository(IRocksDbContext rocksDbContext)
        {
            _rocksDbContext = rocksDbContext;
        }

        public IHashTrieNode GetNode(ulong id)
        {
            if(id==0) Console.WriteLine("0000000000000") ;
            var prefix = EntryPrefix.PersistentHashMap.BuildPrefix(id);
            var raw = _rocksDbContext.Get(prefix);
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

        public bool NodeIdExist(ulong id)
        {
            var prefix =  EntryPrefix.KeepRecentSnapshot.BuildPrefix(id);
            if(_rocksDbContext.Get(prefix) is null) return false;
            return true;
        }

        public void WriteNodeId(ulong id , RocksDbAtomicWrite batch)
        {
            // saving nodes that are reachable from recent snapshots temporarily
            // so that all other nodes can be deleted. 
            var prefix = EntryPrefix.KeepRecentSnapshot.BuildPrefix(id);
            batch.Put(prefix, new byte[1]);
        }

        public void DeleteNodeId(ulong id , RocksDbAtomicWrite batch)
        {
            // Deleting temporary nodes
            var prefix = EntryPrefix.KeepRecentSnapshot.BuildPrefix(id);
            batch.Delete(prefix);
        }

        public void DeleteNode(ulong id , IHashTrieNode node , RocksDbAtomicWrite batch)
        {
            var prefix = EntryPrefix.PersistentHashMap.BuildPrefix(id);
            batch.Delete(prefix);
            prefix = EntryPrefix.VersionByHash.BuildPrefix(node.Hash);
            batch.Delete(prefix);
        }
    }
}