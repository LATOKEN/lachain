namespace Lachain.Storage.DbCompact
{
    public enum DbShrinkStatus : byte
    {
        Stopped = 0,
        SaveTempNodeInfo = 1,
        DeleteOldSnapshot = 2,
        AsyncDeletionStarted = 3,
        DeletionStep1Complete = 4,
        DeletionStep2Complete = 5,
        DeleteTempNodeInfo = 6,
        CheckConsistency = 7,
    }
}