using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lachain.Storage.Trie
{
    public class NodeRetrieval : INodeRetrieval
    {
        private readonly IRocksDbContext _rocksDbContext;

        public NodeRetrieval(IRocksDbContext rocksDbContext)
        {
            _rocksDbContext = rocksDbContext;
        }

        public IHashTrieNode? TryGetNode(ulong id)
        {
            var prefix = EntryPrefix.PersistentHashMap.BuildPrefix(id);
            var raw = _rocksDbContext.Get(prefix);
            if (prefix == null) return null;
            return NodeSerializer.FromBytes(raw);
        }
    }
}
