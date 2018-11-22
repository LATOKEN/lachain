using System;
using System.Collections.Generic;
using Google.Protobuf;
using Phorkus.Proto;
using Phorkus.Core.Storage;
using Phorkus.Core.Utils;

namespace Phorkus.RocksDB.Repositories
{
    public class AssetRepository : IAssetRepository
    {
        private readonly IRocksDbContext _rocksDbContext;

        public AssetRepository(
            IRocksDbContext rocksDbContext)
        {
            _rocksDbContext = rocksDbContext ?? throw new ArgumentNullException(nameof(rocksDbContext));
        }

        public IEnumerable<UInt160> GetAssetHashes()
        {
            return _GetAssetHashes();
        }

        public IEnumerable<string> GetAssetNames()
        {
            return _GetAssetNames();
        }

        public Asset GetAssetByHash(UInt160 assetHash)
        {
            return _GetAssetByHash(assetHash);
        }

        public Asset GetAssetByName(string assetName)
        {
            return _GetAssetByHash(_GetHashByName(assetName));
        }

        public bool AddAsset(Asset asset)
        {
            if (!_IsAssetValid(asset) || _IsAssetAlreadyExists(asset))
                return false;
            var hash = asset.Hash;
            /* write asset by hash */
            var prefixByHash = EntryPrefix.AssetByHash.BuildPrefix(hash);
            _rocksDbContext.Save(prefixByHash, asset.ToByteArray());
            /* write asset hash by name */
            var prefixByName = EntryPrefix.AssetHashByName.BuildPrefix(asset.Name);
            _rocksDbContext.Save(prefixByName, hash.ToByteArray());
            /* update asset hashes */
            lock (_rocksDbContext)
            {
                var hashes = _GetAssetHashes(() => new List<UInt160>());
                hashes.Add(hash);
                _SetAssetHashes(hashes);
            }
            /* update asset hashes */
            lock (_rocksDbContext)
            {
                var names = _GetAssetNames(() => new List<string>());
                names.Add(asset.Name);
                _SetAssetNames(names);
            }
            return true;
        }

        public bool DeleteAssetByHash(UInt160 assetHash)
        {
            var asset = _GetAssetByHash(assetHash);
            if (asset == null)
                return false;
            /* write asset by hash */
            var prefixByHash = EntryPrefix.AssetByHash.BuildPrefix(assetHash);
            _rocksDbContext.Delete(prefixByHash);
            /* write asset hash by name */
            var prefixByName = EntryPrefix.AssetHashByName.BuildPrefix(asset.Name);
            _rocksDbContext.Delete(prefixByName);
            /* update asset hashes */
            lock (_rocksDbContext)
            {
                var hashes = _GetAssetHashes(() => new List<UInt160>());
                hashes.Remove(assetHash);
                _SetAssetHashes(hashes);
            }
            /* update asset hashes */
            lock (_rocksDbContext)
            {
                var names = _GetAssetNames(() => new List<string>());
                names.Remove(asset.Name);
                _SetAssetNames(names);
            }
            return true;
        }

        private ICollection<UInt160> _GetAssetHashes(Func<ICollection<UInt160>> defaultValue = null)
        {
            if (defaultValue == null)
                defaultValue = Array.Empty<UInt160>;
            var prefix = EntryPrefix.AssetHashes.BuildPrefix();
            var raw = _rocksDbContext.Get(prefix);
            if (raw == null)
                return defaultValue();
            var result = raw.ToMessageArray<UInt160>();
            return result ?? defaultValue();
        }

        private void _SetAssetHashes(IEnumerable<UInt160> hashes)
        {
            var raw = hashes.ToByteArray();
            _rocksDbContext.Save(EntryPrefix.AssetHashes.BuildPrefix(), raw);
        }

        private ICollection<string> _GetAssetNames(Func<ICollection<string>> defaultValue = null)
        {
            if (defaultValue == null)
                defaultValue = Array.Empty<string>;
            var prefix = EntryPrefix.AssetNames.BuildPrefix();
            var raw = _rocksDbContext.Get(prefix);
            if (raw == null)
                return defaultValue();
            var result = raw.ToStringArray();
            return result ?? defaultValue();
        }
        
        private void _SetAssetNames(IEnumerable<string> names)
        {
            var raw = names.ToByteArray();
            _rocksDbContext.Save(EntryPrefix.AssetNames.BuildPrefix(), raw);
        }
        
        private Asset _GetAssetByHash(UInt160 assetHash)
        {
            var prefix = EntryPrefix.AssetByHash.BuildPrefix(assetHash);
            var raw = _rocksDbContext.Get(prefix);
            if (raw == null)
                return null;
            var result = Asset.Parser.ParseFrom(raw);
            return result;
        }

        private UInt160 _GetHashByName(string assetName)
        {
            var prefix = EntryPrefix.AssetHashByName.BuildPrefix(assetName);
            var raw = _rocksDbContext.Get(prefix);
            if (raw == null)
                return null;
            var hash = UInt160.Parser.ParseFrom(raw);
            return hash;
        }

        private bool _IsAssetValid(Asset asset)
        {
            return true;
        }

        private bool _IsAssetAlreadyExists(Asset asset)
        {
            var hashPrefix = EntryPrefix.AssetByHash.BuildPrefix(asset.Hash);
            var hashRaw = _rocksDbContext.Get(hashPrefix);
            if (hashRaw != null)
                return true;
            var namePrefix = EntryPrefix.AssetByHash.BuildPrefix(asset.Name);
            var nameRaw = _rocksDbContext.Get(namePrefix);
            if (nameRaw != null)
                return true;
            return false;
        }
    }
}