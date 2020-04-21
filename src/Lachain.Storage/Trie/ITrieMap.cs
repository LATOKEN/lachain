using System.Collections.Generic;
using Lachain.Proto;

namespace Lachain.Storage.Trie
{
    public interface ITrieMap
    {
        void Checkpoint(ulong root);
        void ClearCaches();
        ulong Add(ulong root, byte[] key, byte[] value);
        ulong AddOrUpdate(ulong root, byte[] key, byte[] value);
        ulong Update(ulong root, byte[] key, byte[] value);
        ulong Delete(ulong root, byte[] key, out byte[]? value);
        ulong TryDelete(ulong root, byte[] key, out byte[]? value);
        byte[]? Find(ulong root, byte[] key);
        IEnumerable<byte[]> GetValues(ulong root);
        UInt256 GetHash(ulong root);
    }
}