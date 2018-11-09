using System.Collections.Generic;
using System.Threading.Tasks;
using NeoSharp.Core.Models;
using NeoSharp.Types;

namespace NeoSharp.Core.Storage.Blockchain
{
    public interface IAssetRepository
    {
        /// <summary>
        /// Returns available asset hashes
        /// </summary>
        /// <returns></returns>
        Task<IEnumerable<UInt160>> GetAssetHashes();
        
        /// <summary>
        /// Retrieves an assetId by its assetId
        /// </summary>
        /// <param name="assetHash"></param>
        /// <returns></returns>
        Task<Asset> GetAssetByHash(UInt160 assetHash);
        
        /// <summary>
        /// Adds an asset indexed by its assetId
        /// </summary>
        /// <param name="asset"></param>
        /// <returns></returns>
        Task AddAsset(Asset asset);
        
        /// <summary>
        /// Deletes an asset
        /// </summary>
        /// <param name="assetHash"></param>
        /// <returns></returns>
        Task DeleteAssetByHash(UInt160 assetHash);
    }
}