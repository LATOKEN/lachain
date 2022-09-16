namespace Lachain.Utility
{
    // most priority message should have the smallest number
    public enum NetworkMessagePriority : byte
    {
        ConsensusMessage = 0,
        PeerSyncMessage = 1,
        FastSyncMessage = 2,
        PoolSyncMessage = 3,
        ReplyMessage = 4,
        Others = 5,
    }
}