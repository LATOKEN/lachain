using Lachain.Proto;
using Lachain.Storage.Trie;
using System.Collections.Generic;

namespace Lachain.Storage.State
{
    public interface ISnapshot
    {
        ulong Version { get; }
        void Commit();
        UInt256 Hash { get; }
        IDictionary<ulong,IHashTrieNode> GetState();
        bool IsTrieNodeHashesOk();

        ulong SetState(ulong root, IDictionary<ulong, IHashTrieNode> allTrieNodes);
    }
}