namespace Phorkus.RocksDB
{
    public enum EntryPrefix : short
    {        
        AssetByHash = 0x0201,
        AssetHashByName = 0x0202,
        AssetHashes = 0x0203,
        AssetNames = 0x0204,
        
        BlockByHash = 0x0301,
        BlockHashByHeight = 0x0302,
        
    }
}