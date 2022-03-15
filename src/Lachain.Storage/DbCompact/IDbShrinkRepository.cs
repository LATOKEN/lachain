using Lachain.Storage.Trie;

namespace Lachain.Storage.DbCompact
{
    public interface IDbShrinkRepository
    {
        bool NodeIdExists(ulong id);
        void WriteNodeId(ulong id);
        void DeleteNodeId(ulong id);
        IHashTrieNode? GetNodeById(ulong id);
        void DeleteNode(ulong id, IHashTrieNode node);
        void DeleteVersion(uint repository, ulong block, ulong version);
        void SetDbShrinkStatus(DbShrinkStatus status);
        void SetDbShrinkDepth(ulong depth);
        ulong? GetDbShrinkDepth();
        DbShrinkStatus GetDbShrinkStatus();
        ulong GetOldestSnapshotInDb();
        void SetOldestSnapshotInDb(ulong block);
        void DeleteStatusAndDepth();
    }
}