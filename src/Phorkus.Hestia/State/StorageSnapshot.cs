using Phorkus.Core.Blockchain;
using Phorkus.Core.Blockchain.State;
using Phorkus.Hestia.Repositories;

namespace Phorkus.Hestia.State
{
    public class StorageSnapshot : IStorageSnapshot, ISnapshot
    {
        private readonly IStorageState _state;

        internal StorageSnapshot(IStorageState state)
        {
            _state = state;
        }

        public ulong Version => _state.CurrentVersion;


        public void Commit()
        {
            _state.Commit();
        }


        public StorageValue GetStorage(StorageKey key)
        {
            /*var raw = _state.Get(key.BuildStateStorageKey());
            return raw == null
                ? null
                : _binarySerializer.Deserialize<StorageValue>(raw);*/
            throw new System.NotImplementedException();
        }

        public void AddOrUpdateStorage(StorageKey key, StorageValue val)
        {
            /*_state.AddOrUpdate(key.BuildStateStorageKey(), val.Value);*/
            throw new System.NotImplementedException();
        }

        public void DeleteStorage(StorageKey key)
        {
            /*_state.Delete(key.BuildStateStorageKey());*/
            throw new System.NotImplementedException();
        }
    }
}