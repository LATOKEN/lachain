using System;
using NeoSharp.BinarySerialization;
using NeoSharp.Core.Models;
using NeoSharp.Core.Storage.Blockchain;

namespace NeoSharp.RocksDB.Repositories
{
    public class StorageRepository : IStorageRepository
    {
        private readonly IBinarySerializer _binarySerializer;
        private readonly IRocksDbContext _rocksDbContext;

        public StorageRepository(
            IRocksDbContext rocksDbContext,
            IBinarySerializer binarySerializer)
        {
            _binarySerializer = binarySerializer ?? throw new ArgumentNullException(nameof(binarySerializer));
            _rocksDbContext = rocksDbContext ?? throw new ArgumentNullException(nameof(rocksDbContext));
        }


        public StorageValue GetStorage(StorageKey key)
        {
            var raw = _rocksDbContext.Get(key.BuildStateStorageKey());
            return raw == null
                ? null
                : _binarySerializer.Deserialize<StorageValue>(raw);
        }

        public void AddStorage(StorageKey key, StorageValue val)
        {
            _rocksDbContext.Save(key.BuildStateStorageKey(), val.Value);
        }
        
        public void DeleteStorage(StorageKey key)
        {
            _rocksDbContext.Delete(key.BuildStateStorageKey());
        }
    }
}