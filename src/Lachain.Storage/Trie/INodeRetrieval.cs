using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lachain.Storage.Trie
{
    public interface INodeRetrieval
    {
        public IHashTrieNode? TryGetNode(ulong id);
    }
}
