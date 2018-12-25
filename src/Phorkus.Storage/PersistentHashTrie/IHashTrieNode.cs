using System.Collections.Generic;

namespace Phorkus.Storage.PersistentHashTrie
{
    public interface IHashTrieNode
    {
        NodeType Type { get; }
        ulong GetChildByHash(byte h);
        IEnumerable<ulong> Children { get; }
    }
}