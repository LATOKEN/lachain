using System.Collections.Generic;
using Lachain.Proto;
using Lachain.Storage.Trie;

namespace Lachain.Storage
{
    public interface IStorageState
    {
        ulong CurrentVersion { get; }
        
        byte[]? Get(byte[] key);
        ulong Add(byte[] key, byte[] value);
        ulong AddOrUpdate(byte[] key, byte[] value);
        ulong Update(byte[] key, byte[] value);
        ulong Delete(byte[] key, out byte[]? value);
        ulong TryDelete(byte[] key, out byte[]? value);
        
        IDictionary<ulong, IHashTrieNode> GetAllNodes();
        public bool IsNodeHashesOk();

        IEnumerable<byte[]> Values { get; }
        
        UInt256 Hash { get; }
        
        public ulong InsertAllNodes(ulong root, IDictionary<ulong, IHashTrieNode> allTrieNodes);
        public void SetCurrentVersion(ulong root);
        
        ulong Commit();
        ulong Cancel();
    }
}