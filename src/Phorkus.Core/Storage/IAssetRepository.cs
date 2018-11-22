using System.Collections.Generic;
using Phorkus.Core.Proto;

namespace Phorkus.Core.Storage
{
    public interface IAssetRepository
    {
        /// <summary>
        /// Returns available asset hashes
        /// </summary>
        /// <returns></returns>
        IEnumerable<UInt160> GetAssetHashes();
        
        /// <summary>
        /// Returns available asset names
        /// </summary>
        /// <returns></returns>
        IEnumerable<string> GetAssetNames();
        
        /// <summary>
        /// Retrieves an assetId by its assetId
        /// </summary>
        /// <param name="assetHash"></param>
        /// <returns></returns>
        Asset GetAssetByHash(UInt160 assetHash);

        /// <summary>
        /// Retrieves an assetId by its assetId
        /// </summary>
        /// <param name="assetName"></param>
        /// <returns></returns>
        Asset GetAssetByName(string assetName);
        
        /// <summary>
        /// Adds an asset indexed by its assetId
        /// </summary>
        /// <param name="asset"></param>
        /// <returns></returns>
        bool AddAsset(Asset asset);
        
        /// <summary>
        /// Deletes an asset
        /// </summary>
        /// <param name="assetHash"></param>
        /// <returns></returns>
        bool DeleteAssetByHash(UInt160 assetHash);
    }
}