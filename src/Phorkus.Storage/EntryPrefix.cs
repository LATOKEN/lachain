namespace Phorkus.Storage
{
    public enum EntryPrefix : short
    {
        Global = 0x0101,
        
        AssetByHash = 0x0201,
        AssetHashByName = 0x0202,
        AssetHashes = 0x0203,
        AssetNames = 0x0204,
        AssetSupplyByHash = 0x0205,
        
        BlockByHash = 0x0301,
        BlockHashByHeight = 0x0302,
        BlockHeight = 0x0303,
        
        TransactionByHash = 0x0401,
        TransactionCountByFrom = 0x0402,
        TransactionPool = 0x0404,
        
        BalanceByOwnerAndAsset = 0x0501,
        WithdrawingBalanceByOwnerAndAsset = 0x0511,
        
        StorageVersionIndex = 0x0601,
        PersistentTreeMap = 0x0602,
        PersistentHashMap = 0x0603,
        
        WithdrawalByHash = 0x0701,
        WithdrawalByNonce = 0x0702,
        WithdrawalByStateNonce = 0x0703,
        WithdrawalNonce = 0x0704,
        
        ContractByHash = 0x0801,
        ContractCountByFrom = 0x0802,
        
        ContractStorage = 0x903,
    }
}