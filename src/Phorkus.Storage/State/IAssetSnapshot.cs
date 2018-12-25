using System.Collections.Generic;
using Phorkus.Proto;

namespace Phorkus.Storage.State
{
    public interface IAssetSnapshot
    {
        Asset GetAssetByHash(UInt160 assetHash);
        
        bool AddAsset(Asset asset);
        
        Asset GetAssetByName(string assetName);
        
        IEnumerable<UInt160> GetAssetHashes();
        
        IEnumerable<string> GetAssetNames();
        
        IEnumerable<Asset> GetAssets();
    }
}