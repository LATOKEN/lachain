namespace Lachain.Storage.DbCompact
{
    public interface IDbShrink
    {
        ulong GetOldestSnapshotInDb();
        ulong StartingBlockToKeep(ulong depth, ulong totalBlocks);
        bool IsStopped();
        DbShrinkStatus GetDbShrinkStatus();
        ulong? GetDbShrinkDepth();
        void ShrinkDb(ulong depth, ulong totalBlocks, bool consistencyCheck);
    }
}