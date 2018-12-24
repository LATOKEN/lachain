using System.Collections.Generic;
using System.Linq;
using System.Text;
using Google.Protobuf;
using Phorkus.Core.Blockchain.State;
using Phorkus.Hestia.Repositories;
using Phorkus.Proto;
using Phorkus.RocksDB;

namespace Phorkus.Hestia.State
{
    public class AssetSnapshot : IAssetSnapshot, ISnapshot
    {
        private readonly IStorageState _state;

        internal AssetSnapshot(IStorageState state)
        {
            _state = state;
        }

        public ulong Version => _state.CurrentVersion;


        public void Commit()
        {
            _state.Commit();
        }

        public IEnumerable<UInt160> GetAssetHashes()
        {
            return _state.Entries.SelectMany(pair =>
            {
                if (EntryPrefix.AssetByHash.Matches(pair.Key))
                    return new[] {UInt160.Parser.ParseFrom(pair.Key.Skip(2).ToArray())};
                return Enumerable.Empty<UInt160>();
            });
        }

        public IEnumerable<string> GetAssetNames()
        {
            return _state.Entries.SelectMany(pair =>
            {
                if (EntryPrefix.AssetHashByName.Matches(pair.Key))
                    return new[] {Encoding.ASCII.GetString(pair.Key.Skip(2).ToArray())};
                return Enumerable.Empty<string>();
            });
        }

        public IEnumerable<Asset> GetAssets()
        {
            return _state.Entries.SelectMany(pair =>
            {
                if (EntryPrefix.AssetByHash.Matches(pair.Key))
                    return new[] {Asset.Parser.ParseFrom(pair.Value)};
                return Enumerable.Empty<Asset>();
            });
        }

        public Asset GetAssetByHash(UInt160 assetHash)
        {
            var prefix = EntryPrefix.AssetByHash.BuildPrefix(assetHash);
            var raw = _state.Get(prefix);
            if (raw == null)
                return null;
            var result = Asset.Parser.ParseFrom(raw);
            return result;
        }

        public Asset GetAssetByName(string assetName)
        {
            var assetHash = _GetHashByName(assetName);
            if (assetHash is null)
                return null;
            return GetAssetByHash(assetHash);
        }

        public bool AddAsset(Asset asset)
        {
            if (!_IsAssetValid(asset) || _IsAssetAlreadyExists(asset))
                return false;
            var hash = asset.Hash;
            /* write asset by hash */
            var prefixByHash = EntryPrefix.AssetByHash.BuildPrefix(hash);
            _state.Add(prefixByHash, asset.ToByteArray());
            /* write asset hash by name */
            var prefixByName = EntryPrefix.AssetHashByName.BuildPrefix(asset.Name);
            _state.Add(prefixByName, hash.ToByteArray());
            return true;
        }

        private bool _IsAssetValid(Asset asset)
        {
            return true;
        }

        private bool _IsAssetAlreadyExists(Asset asset)
        {
            var hashPrefix = EntryPrefix.AssetByHash.BuildPrefix(asset.Hash);
            var hashRaw = _state.Get(hashPrefix);
            if (hashRaw != null)
                return true;
            var namePrefix = EntryPrefix.AssetByHash.BuildPrefix(asset.Name);
            var nameRaw = _state.Get(namePrefix);
            return nameRaw != null;
        }

        private UInt160 _GetHashByName(string assetName)
        {
            var prefix = EntryPrefix.AssetHashByName.BuildPrefix(assetName);
            var raw = _state.Get(prefix);
            if (raw == null)
                return null;
            var hash = UInt160.Parser.ParseFrom(raw);
            return hash;
        }
    }
}