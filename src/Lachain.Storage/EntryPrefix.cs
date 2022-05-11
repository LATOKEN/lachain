namespace Lachain.Storage
{
    /*
        There is a single database for the node. So for different use, we divide the space 
        in terms of prefix of 2 byte. 
        
        For example, to store a trie node in the database, we concat PersistentHashMap (= 0x0603) 
        and node id to generate the key and store the node to this key. 
        Similarly to get a node from the database with a given id, we generate the key by concat 
        and query for this key. 
    */
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

        /*fast_sync status*/
        BlockNumber = 0x0701,
        LastDownloaded = 0x0702,
        SavedBatch = 0x0703,
        TotalBatch = 0x0704,
        QueueBatch = 0x0705,

        /* checkpoint info */
        CheckpointBlockHeight = 0x0f01,
        CheckpointBlockHash = 0x0f02,
        CheckpointSnapshotState = 0x0f03,

    }
}