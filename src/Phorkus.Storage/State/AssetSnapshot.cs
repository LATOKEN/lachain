using System.Collections.Generic;
using System.Linq;
using System.Text;
using Google.Protobuf;
using Phorkus.Proto;
using Phorkus.Utility;
using Phorkus.Utility.Utils;

namespace Phorkus.Storage.State
{
    public class AssetSnapshot : IAssetSnapshot
    {
        private readonly IStorageState _state;

        public AssetSnapshot(IStorageState state)
        {
            _state = state;
        }

        public ulong Version => _state.CurrentVersion;

        public void Commit()
        {
            _state.Commit();
        }

        public UInt256 Hash => _state.Hash;

        public IEnumerable<UInt160> GetAssetHashes()
        {
            return GetAssets().Select(asset => asset.Hash);
        }

        public IEnumerable<string> GetAssetNames()
        {
            return GetAssets().Select(asset => asset.Name);
        }

        public IEnumerable<Asset> GetAssets()
        {
            return _state.Values.SelectMany(value =>
            {
                /* TODO: "fix me please" */
                try
                {
                    return new[] {Asset.Parser.ParseFrom(value)};
                }
                catch
                {
                    return Enumerable.Empty<Asset>();
                }
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

        public Money GetAssetSupplyByHash(UInt160 assetHash)
        {
            var key = EntryPrefix.AssetSupplyByHash.BuildPrefix(assetHash);
            var value = _state.Get(key);
            var supply = value != null ? UInt256.Parser.ParseFrom(value) : UInt256Utils.Zero;
            return new Money(supply);
        }

        public Money AddSupply(UInt160 asset, Money value)
        {
            var supply = GetAssetSupplyByHash(asset);
            supply += value;
            ChangeSupply(asset, supply);
            return supply;
        }

        public Money SubSupply(UInt160 asset, Money value)
        {
            var supply = GetAssetSupplyByHash(asset);
            supply -= value;
            ChangeSupply(asset, supply);
            return supply;
        }

        private void ChangeSupply(UInt160 asset, Money amount)
        {
            var key = EntryPrefix.AssetSupplyByHash.BuildPrefix(asset);
            var value = amount.ToUInt256().ToByteArray();
            _state.AddOrUpdate(key, value);
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