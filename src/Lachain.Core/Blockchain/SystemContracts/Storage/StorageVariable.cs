using Lachain.Proto;
using Lachain.Storage.State;

namespace Lachain.Core.Blockchain.SystemContracts.Storage
{
    public class StorageVariable
    {
        private readonly UInt160 _contract;
        private readonly IStorageSnapshot _snapshot;
        private readonly UInt256 _location;

        public StorageVariable(UInt160 contract, IStorageSnapshot snapshot, UInt256 location)
        {
            _contract = contract;
            _snapshot = snapshot;
            _location = location;
        }

        public byte[] Get()
        {
            return _snapshot.GetRawValue(_contract, _location.Buffer);
        }

        public void Set(byte[] value)
        {
            _snapshot.SetRawValue(_contract, _location.Buffer, value);
        }
    }
}