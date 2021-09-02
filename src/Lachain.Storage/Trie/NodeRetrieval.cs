using System;
using System.Collections.Generic;
using Lachain.Utility.Utils;
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
            if (raw == null) return null;
            return NodeSerializer.FromBytes(raw);
        }

        public IHashTrieNode? TryGetNode(byte[] nodeHash, out List<byte[]> childrenHash)
        {
            childrenHash = new List<byte[]>();
            var prefix = EntryPrefix.VersionByHash.BuildPrefix(nodeHash);
            var idByte = _rocksDbContext.Get(prefix);
            if (idByte == null) return null;
            ulong id = UInt64Utils.FromBytes(idByte);
            IHashTrieNode? node = TryGetNode(id);
            if (node == null) return null;

            if(node.Type == NodeType.Internal)
            {
                foreach (var childId in node.Children)
                {
                    var child = TryGetNode(childId);
                    if (child == null) return null;
                    childrenHash.Add(child.Hash);
                }
            }
            return node;
        }
    }
}
