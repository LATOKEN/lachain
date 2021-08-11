using System.Collections.Generic;

namespace Lachain.Storage.Trie
{
    public interface IHashTrieNode
    {
        NodeType Type { get; }

        byte[] Hash { get; }
        
        IEnumerable<ulong> Children { get; }
        
        ulong GetChildByHash(byte h);
    }
}