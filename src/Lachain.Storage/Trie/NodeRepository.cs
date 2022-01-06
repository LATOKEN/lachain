using RocksDbSharp;
using System;
using Lachain.Utility.Utils;
using System.Collections.Generic;
using System.Linq;

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

        public IDictionary<ulong, IHashTrieNode> GetNodes(IEnumerable<ulong> ids)
        {
            if(ids.ToList().Count() == 0) return new Dictionary<ulong, IHashTrieNode>();
            List<byte[]> prefixAddedIds = new List<byte[]>();
            foreach(var id in ids)
            {
                prefixAddedIds.Add(EntryPrefix.PersistentHashMap.BuildPrefix(id));
            }
            var raw = _rocksDbContext.GetMany(prefixAddedIds);
            IDictionary<ulong, IHashTrieNode> result = new Dictionary<ulong, IHashTrieNode>();
            foreach(var item in raw)
            {
                ulong key = (ulong)0;
                for(int i = 9; i >= 2; i--)
                {
                    key = ((key<<8)|item.Key[i]);
                }
                result.Add(key, NodeSerializer.FromBytes(item.Value));
            }
            
            /*
            
            Console.WriteLine("input:");
            foreach(var id in ids) Console.WriteLine(" "+id);
            Console.WriteLine("\n");

            Console.WriteLine("output:");
            foreach(var item in result) Console.WriteLine(" "+item.Key);
            Console.WriteLine("\n");

            */

            return result;
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
    }
}