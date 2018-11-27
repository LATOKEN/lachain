using System;
using Phorkus.Core.Blockchain;
using Phorkus.Core.Storage;
using Phorkus.Core.Storage.Repositories;

namespace Phorkus.RocksDB.Repositories
{
    public class StorageRepository : IStorageRepository
    {
        private readonly IRocksDbContext _rocksDbContext;

        public StorageRepository(IRocksDbContext rocksDbContext)
        {
            _rocksDbContext = rocksDbContext ?? throw new ArgumentNullException(nameof(rocksDbContext));
        }
        
        public StorageValue GetStorage(StorageKey key)
        {
            throw new NotImplementedException();
            /*var raw = _rocksDbContext.Get(key.BuildStateStorageKey());
            return raw == null
                ? null
                : _binarySerializer.Deserialize<StorageValue>(raw);*/
        }

        public void AddStorage(StorageKey key, StorageValue val)
        {
            throw new NotImplementedException();
            /*_rocksDbContext.Save(key.BuildStateStorageKey(), val.Value);*/
        }
        
        public void DeleteStorage(StorageKey key)
        {
            throw new NotImplementedException();
            /*_rocksDbContext.Delete(key.BuildStateStorageKey());*/
        }
    }
}