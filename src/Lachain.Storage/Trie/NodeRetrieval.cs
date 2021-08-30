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
            if (prefix == null) return null;
            return NodeSerializer.FromBytes(raw);
        }

        public IHashTrieNode? TryGetNode(byte[] nodeHash, out List<byte[]> childrenHash)
        {
            childrenHash = new List<byte[]>();
            var prefix = EntryPrefix.VersionByHash.BuildPrefix(nodeHash);
            ulong id = UInt64Utils.FromBytes(_rocksDbContext.Get(prefix));
            if (prefix == null) return null;
            IHashTrieNode? node = TryGetNode(id);

            if(node?.Type==NodeType.Internal){
                InternalNode internalNode = (InternalNode)node;
                foreach(var child in node.Children) childrenHash.Add(TryGetNode(child).Hash);
            }
            return node;
        }
    }
}
