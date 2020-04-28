using System.Collections.Generic;
using System.Linq;
using Lachain.Proto;
using Lachain.Storage.State;

namespace Lachain.Core.Blockchain.SystemContracts.Storage
{
    public class StorageMapping
    {
        private readonly UInt160 _contract;
        private readonly IStorageSnapshot _snapshot;
        private readonly UInt256 _location;

        public StorageMapping(UInt160 contract, IStorageSnapshot snapshot, UInt256 location)
        {
            _contract = contract;
            _snapshot = snapshot;
            _location = location;
        }

        public byte[] GetValue(IEnumerable<byte> key)
        {
            return _snapshot.GetRawValue(_contract, _location.Buffer.Concat(key));
        }

        public void SetValue(IEnumerable<byte> key, byte[] value)
        {
            _snapshot.SetRawValue(_contract, _location.Buffer.Concat(key), value);
        }
    }
}