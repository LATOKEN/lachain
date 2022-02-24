using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Lachain.Proto;
using Lachain.Utility.Utils;
using Lachain.Storage.Trie;

namespace Lachain.Storage.State
{
    class StorageSnapshot : IStorageSnapshot
    {
        private readonly IStorageState _state;

        public ulong Version => _state.CurrentVersion;

        public StorageSnapshot(IStorageState state)
        {
            _state = state;
        }

        public IDictionary<ulong,IHashTrieNode> GetState()
        {
            return _state.GetAllNodes();
        }
        
        public bool IsTrieNodeHashesOk()
        {
            return _state.IsNodeHashesOk();
        }
        public ulong SetState(ulong root, IDictionary<ulong, IHashTrieNode> allTrieNodes)
        {
            return _state.InsertAllNodes(root, allTrieNodes);
        }
        
        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Commit(RocksDbAtomicWrite batch)
        {
            _state.Commit(batch);
        }

        public UInt256 Hash => _state.Hash;

        public UInt256 GetValue(UInt160 contract, UInt256 key)
        {
            var value = _state.Get(
                EntryPrefix.StorageByHash.BuildPrefix(contract).Concat(key.ToBytes()).ToArray()
            );
            return value is null ? UInt256Utils.Zero : value.ToUInt256();
        }

        public void SetValue(UInt160 contract, UInt256 key, UInt256 value)
        {
            _state.AddOrUpdate(
                EntryPrefix.StorageByHash.BuildPrefix(contract).Concat(key.ToBytes()).ToArray(),
                value.ToBytes()
            );
        }

        public void DeleteValue(UInt160 contract, UInt256 key, out UInt256 value)
        {
            _state.TryDelete(
                EntryPrefix.StorageByHash.BuildPrefix(contract).Concat(key.ToBytes()).ToArray(),
                out var buffer
            );
            value = buffer is null ? UInt256Utils.Zero : buffer.ToUInt256();
        }

        public byte[] GetRawValue(UInt160 contract, IEnumerable<byte> key)
        {
            return _state.Get(EntryPrefix.StorageByHash.BuildPrefix(contract).Concat(key).ToArray()) ?? new byte[] { };
        }

        public void SetRawValue(UInt160 contract, IEnumerable<byte> key, byte[] value)
        {
            _state.AddOrUpdate(
                EntryPrefix.StorageByHash.BuildPrefix(contract).Concat(key).ToArray(),
                value
            );
        }

        public void DeleteRawValue(UInt160 contract, IEnumerable<byte> key)
        {
            _state.TryDelete(EntryPrefix.StorageByHash.BuildPrefix(contract).Concat(key).ToArray(), out _);
        }

        public void SetCurrentVersion(ulong root)
        {
            _state.SetCurrentVersion(root);
        }

        public void ClearCache()
        {
            _state.ClearCache();
        }

        public void SaveNodeId(RocksDbAtomicWrite batch)
        {
            _state.SaveNodeId(batch);
        }
    }
}