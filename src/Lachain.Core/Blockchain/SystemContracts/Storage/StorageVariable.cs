using Lachain.Proto;
using Lachain.Storage.State;

namespace Lachain.Core.Blockchain.SystemContracts.Storage
{

    /*    
        StorageVariable is a variable that reside in the state of the blockchain.
        It's a part of the Storage Trie of the state. A contract can declare and use a StorageVariable
        for its use and this variable will be part of the state.
    */
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