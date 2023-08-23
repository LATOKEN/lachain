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

        QueueBatch = 0x0605,

        NodesDownloadedTillNow = 0x0606,


        /* banned peers */
        BannedPeerLowestCycle = 0x0701,
        BannedPeerListByCycle = 0x0702,
        BannedPeerVoteLowestCycle = 0x0703,
        BannedPeerVotersByCycle = 0x0704,
        VotedPeerListByCycle = 0x0705,

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
        MessageEnvelope = 0xb03,

        /* validator attendance */
        ValidatorAttendanceState = 0x0c01,

        /* local transactions */
        LocalTransactionsState = 0x0d01,

        /* validator attendance */
        PeerList = 0x0e01,
        PeerByPublicKey = 0x0e01,

        /* db shrink */
        NodeIdForRecentSnapshot = 0x0f01,
        NodeHashForRecentSnapshot = 0x0f02,
        DbShrinkStatus = 0x0f03,
        DbShrinkDepth = 0x0f04,
        OldestSnapshotInDb = 0x0f05,
        TimePassedMillis = 0x0f06,
        LastSavedTimeMillis = 0x0f07,
        TotalTempKeysSaved = 0x0f08,
        TotalNodesDeleted = 0x0f09,
    }
}