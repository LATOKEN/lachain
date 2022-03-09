using System.Collections.Generic;
using Lachain.Proto;
using RocksDbSharp;
using Lachain.Storage.DbCompact;

namespace Lachain.Storage.Trie
{
    public interface ITrieMap
    {
        void Checkpoint(ulong root, RocksDbAtomicWrite batch);
        void ClearCaches();
        ulong Add(ulong root, byte[] key, byte[] value);
        ulong AddOrUpdate(ulong root, byte[] key, byte[] value);
        ulong Update(ulong root, byte[] key, byte[] value);
        ulong Delete(ulong root, byte[] key, out byte[]? value);
        ulong TryDelete(ulong root, byte[] key, out byte[]? value);
        byte[]? Find(ulong root, byte[] key);
        IDictionary<ulong, IHashTrieNode> GetAllNodes(ulong root);
        IEnumerable<byte[]> GetValues(ulong root);
        public bool CheckAllNodeHashes(ulong root);
        public ulong InsertAllNodes(ulong root, IDictionary<ulong, IHashTrieNode> allTrieNodes);
        UInt256 GetHash(ulong root);
        ulong UpdateNodeIdToBatch(ulong root, bool save, IDbShrinkRepository _repo);
        ulong DeleteNodes(ulong root, IDbShrinkRepository _repo);
    }
}