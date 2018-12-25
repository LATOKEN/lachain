using System;
using System.Collections.Generic;
using System.Linq;
using RocksDbSharp;

namespace Phorkus.Storage.RocksDB
{
    public class RocksDbContext : IRocksDbContext
    {
        private RocksDb _rocksDb;

        public void Open(StorageConfig storageConfig)
        {
            var options = new DbOptions().SetCreateIfMissing();
            _rocksDb = RocksDb.Open(options, storageConfig.Path);
        }

        public void Close()
        {
            _ThrowIfNotInitialized();
            _rocksDb = null;
        }

        public byte[] Get(byte[] key)
        {
            _ThrowIfNotInitialized();
            return _rocksDb.Get(key);
        }

        public IDictionary<byte[], byte[]> GetMany(IEnumerable<byte[]> keys)
        {
            _ThrowIfNotInitialized();
            return _rocksDb.MultiGet(keys.ToArray()).ToDictionary(kv => kv.Key, k => k.Value);
        }

        public void Save(byte[] key, byte[] content)
        {
            _ThrowIfNotInitialized();
            _rocksDb.Put(key, content);
        }
        
        public void Save(IEnumerable<byte> key, IEnumerable<byte> content)
        {
            _ThrowIfNotInitialized();
            _rocksDb.Put(key.ToArray(), content.ToArray());
        }

        public void Delete(byte[] key)
        {
            _ThrowIfNotInitialized();
            _rocksDb.Remove(key);
        }
        
        public void Dispose()
        {
            _rocksDb?.Dispose();
        }
        
        private void _ThrowIfNotInitialized()
        {
            if (_rocksDb is null)
                throw new Exception("RocksDB database hasn't been opened");
        }
    }
}