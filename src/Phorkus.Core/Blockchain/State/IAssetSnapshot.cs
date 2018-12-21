using System.Collections.Generic;
using Phorkus.Proto;
using Phorkus.Utility;

namespace Phorkus.Core.Blockchain.State
{
    public interface IAssetSnapshot
    {
        Asset GetAssetByHash(UInt160 assetHash);
        bool AddAsset(Asset asset);
        Asset GetAssetByName(string assetName);

        IEnumerable<UInt160> GetAssetHashes();
        IEnumerable<string> GetAssetNames();
        IEnumerable<Asset> GetAssets();
        
        Money GetAssetSupplyByHash(UInt160 assetHash);
        Money AddSupply(UInt160 asset, Money value);
        Money SubSupply(UInt160 asset, Money value);
    }
}