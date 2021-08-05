using RocksDbSharp;
using System;

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
            tx.Delete(prefix) ;
        }

        public void WriteNodeToBatch(ulong id, IHashTrieNode node, RocksDbAtomicWrite tx)
        {
            var prefix = EntryPrefix.PersistentHashMap.BuildPrefix(id);
            tx.Put(prefix, NodeSerializer.ToBytes(node));
        }

        public void SaveBatch(WriteBatch batch)
        {
            _rocksDbContext.SaveBatch(batch);
        }
    }
}