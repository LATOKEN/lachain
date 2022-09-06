using Lachain.Storage.Trie;
using RocksDbSharp;

namespace Lachain.Storage.DbCompact
{
    public interface IDbShrinkRepository
    {
        void Delete(byte[] key, bool tryCommit = true);
        bool NodeIdExists(ulong id);
        void WriteNodeIdAndHash(ulong id, IHashTrieNode node);
        IHashTrieNode? GetNodeById(ulong id);
        void DeleteVersion(uint repository, ulong block);
        ulong TimePassed();
        ulong GetLastSavedTime();
        void UpdateTime();
        void SetDbShrinkStatus(DbShrinkStatus status);
        void SetDbShrinkDepth(ulong depth);
        ulong? GetDbShrinkDepth();
        DbShrinkStatus GetDbShrinkStatus();
        ulong GetOldestSnapshotInDb();
        void SetOldestSnapshotInDb(ulong block);
        void SaveTempKeyCount(ulong count);
        ulong GetTempKeyCount();
        void SaveDeletedNodeCount(ulong count);
        ulong GetTotalNodesDeleted();
        void DeleteAll();
        Iterator? GetIteratorForPrefixOnly(byte[] prefix);
    }
}