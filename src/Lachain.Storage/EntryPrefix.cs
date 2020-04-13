namespace Lachain.Storage
{
    public enum EntryPrefix : short
    {
        /* block */
        BlockByHash = 0x0301,
        BlockHashByHeight = 0x0302,
        BlockHeight = 0x0303,

        /* transaction */
        TransactionByHash = 0x0401,
        TransactionCountByFrom = 0x0402,
        TransactionPool = 0x0404,

        /* balance */
        BalanceByOwnerAndAsset = 0x0501,

        /* storage version & index by height */
        StorageVersionIndex = 0x0601,
        SnapshotIndex = 0x0602,

        /* node */
        PersistentHashMap = 0x0603,

        /* contract */
        ContractByHash = 0x0801,

        /* storage */
        StorageByHash = 0x0903,

        /* event */
        EventByTransactionHashAndIndex = 0x0a01,
        EventCountByTransactionHash = 0x0a02,

        /* consensus */
        ConsensusState = 0x0b01,
        PendingValidators = 0x0b02,
        ConfirmationMessage = 0x0b03,
    }
}