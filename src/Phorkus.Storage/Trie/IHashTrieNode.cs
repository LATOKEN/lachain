using System.Collections.Generic;

namespace Phorkus.Storage.Trie
{
    public interface IHashTrieNode
    {
        NodeType Type { get; }
        
        IEnumerable<ulong> Children { get; }
        
        ulong GetChildByHash(byte h);
    }
}