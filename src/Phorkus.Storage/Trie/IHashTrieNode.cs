using System.Collections.Generic;

namespace Phorkus.Storage.Trie
{
    internal interface IHashTrieNode
    {
        NodeType Type { get; }

        byte[] Hash { get; }
        
        IEnumerable<ulong> Children { get; }
        
        ulong GetChildByHash(byte h);
    }
}