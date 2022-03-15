using Lachain.Storage.Trie;

namespace Lachain.Storage.DbCompact
{
    public interface IDbShrinkRepository
    {
        void Save(byte[] key, byte[] content, bool tryCommit = true);
        void Delete(byte[] key, bool tryCommit = true);
        void Commit();
        byte[]? Get(byte[] key);
        bool KeyExists(byte[] key);
        bool NodeIdExist(ulong id);
        void WriteNodeId(ulong id);
        void DeleteNodeId(ulong id);
        IHashTrieNode? GetNodeById(ulong id);
        void DeleteNode(ulong id, IHashTrieNode node);
    }
}