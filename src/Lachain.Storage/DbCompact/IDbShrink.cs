namespace Lachain.Storage.DbCompact
{
    public interface IDbShrink
    {
        bool IsStopped();
        DbShrinkStatus GetDbShrinkStatus();
        ulong? GetDbShrinkDepth();
        void ShrinkDb(ulong depth, ulong totalBlocks, bool consistencyCheck);
    }
}