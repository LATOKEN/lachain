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

        /* native token */
        BalanceByOwnerAndAsset = 0x0501,
        TotalSupply = 0x0502,
        AllowedSupply = 0x0503,
        MinterAddress = 0x0504,

        /* storage version & index by height */
        StorageVersionIndex = 0x0601,
        SnapshotIndex = 0x0602,

        /* node */
        PersistentHashMap = 0x0603,

        VersionByHash = 0x0604,

        QueueBatch = 0x0605,

        NodesDownloadedTillNow = 0x0606,
        NodeIdForRecentSnapshot = 0x0607,
        DbShrinkStatus = 0x0608,
        DbShrinkDepth = 0x0609,
        OldestSnapshotInDb = 0x060a,

        /* contract */
        ContractByHash = 0x0801,

        /* storage */
        StorageByHash = 0x0903,

        /* event */
        EventByTransactionHashAndIndex = 0x0a01,
        EventCountByTransactionHash = 0x0a02,
        EventTopicsByTransactionHashAndIndex = 0x0a03,

        /* consensus */
        ConsensusState = 0x0b01,
        KeyGenState = 0x0b02,

        /* validator attendance */
        ValidatorAttendanceState = 0x0c01,

        /* local transactions */
        LocalTransactionsState = 0x0d01,

        /* validator attendance */
        PeerList = 0x0e01,
        PeerByPublicKey = 0x0e01,
    }
}