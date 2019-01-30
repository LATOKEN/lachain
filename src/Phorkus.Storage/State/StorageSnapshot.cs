using System.Linq;
using System.Runtime.CompilerServices;
using Phorkus.Proto;
using Phorkus.Utility.Utils;

namespace Phorkus.Storage.State
{
    class StorageSnapshot : IStorageSnapshot
    {
        private readonly IStorageState _state;
        
        public ulong Version => _state.CurrentVersion;

        public StorageSnapshot(IStorageState state)
        {
            _state = state;
        }
        
        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Commit()
        {
            _state.Commit();
        }

        public UInt256 GetValue(UInt160 contract, UInt256 key)
        {
            var value = _state.Get(
                EntryPrefix.ContractStorage.BuildPrefix(contract).Concat(key.Buffer.ToByteArray()).ToArray() 
            );
            return value is null ? UInt256Utils.Zero : value.ToUInt256();
        }

        public void SetValue(UInt160 contract, UInt256 key, UInt256 value)
        {
            _state.AddOrUpdate(
                EntryPrefix.ContractStorage.BuildPrefix(contract).Concat(key.Buffer.ToByteArray()).ToArray(),
                value.Buffer.ToByteArray()
            );
        }
    }
}