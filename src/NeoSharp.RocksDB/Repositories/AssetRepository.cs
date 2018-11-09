using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NeoSharp.BinarySerialization;
using NeoSharp.Core.Models;
using NeoSharp.Core.Storage.Blockchain;
using NeoSharp.Types;

namespace NeoSharp.RocksDB.Repositories
{
    public class AssetRepository : IAssetRepository
    {
        private readonly IBinarySerializer _binarySerializer;
        private readonly IRocksDbContext _rocksDbContext;
        
        public AssetRepository(
            IRocksDbContext rocksDbContext,
            IBinarySerializer binarySerializer)
        {
            _binarySerializer = binarySerializer ?? throw new ArgumentNullException(nameof(binarySerializer));
            _rocksDbContext = rocksDbContext ?? throw new ArgumentNullException(nameof(rocksDbContext));
        }

        public Task<IEnumerable<UInt160>> GetAssetHashes()
        {
            throw new NotImplementedException();
        }

        public async Task<Asset> GetAssetByHash(UInt160 assetHash)
        {
            var raw = await _rocksDbContext.Get(assetHash.BuildStateAssetKey());
            return raw == null ? null : _binarySerializer.Deserialize<Asset>(raw);
        }
        
        public async Task AddAsset(Asset asset)
        {
            await _rocksDbContext.Save(asset.Hash.BuildStateAssetKey(), _binarySerializer.Serialize(asset));
        }

        public async Task DeleteAssetByHash(UInt160 assetHash)
        {
            await _rocksDbContext.Delete(assetHash.BuildStateAssetKey());
        }
    }
}