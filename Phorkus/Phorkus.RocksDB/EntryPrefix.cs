namespace Phorkus.RocksDB
{
    public enum EntryPrefix : short
    {
        Global = 0x0101,
        
        AssetByHash = 0x0201,
        AssetHashByName = 0x0202,
        AssetHashes = 0x0203,
        AssetNames = 0x0204,
        
        BlockByHash = 0x0301,
        BlockHashByHeight = 0x0302,
        
        TransactionByHash = 0x0401,
        TransactionLatestByFrom = 0x0402,
        TransactionStateByHash = 0x0403,
    }
}