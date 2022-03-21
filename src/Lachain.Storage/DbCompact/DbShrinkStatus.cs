namespace Lachain.Storage.DbCompact
{
    public enum DbShrinkStatus : byte
    {
        Stopped = 0,
        SaveNodeId = 1,
        DeleteOldSnapshot = 2,
        DeleteNodeId = 3,
        CheckConsistency = 4,
    }
}