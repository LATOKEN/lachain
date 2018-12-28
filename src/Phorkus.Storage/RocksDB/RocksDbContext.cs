using System;
using System.Collections.Generic;
using System.Linq;
using RocksDbSharp;

namespace Phorkus.Storage.RocksDB
{
    public class RocksDbContext : IRocksDbContext
    {
        private RocksDb _rocksDb;

        public RocksDbContext()
        {
            var options = new DbOptions().SetCreateIfMissing();
            /* TODO: "yeah, fix me please" */
            _rocksDb = RocksDb.Open(options, "ChainPhorkus");
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
            var writeOptions = new WriteOptions();
            writeOptions.DisableWal(0);
            writeOptions.SetSync(true);
            _rocksDb.Put(key, content, null, writeOptions);
        }
        
        public void Save(IEnumerable<byte> key, IEnumerable<byte> content)
        {
            _ThrowIfNotInitialized();
            var writeOptions = new WriteOptions();
            writeOptions.DisableWal(0);
            writeOptions.SetSync(true);
            _rocksDb.Put(key.ToArray(), content.ToArray(), null, writeOptions);
        }

        public void Delete(byte[] key)
        {
            _ThrowIfNotInitialized();
            var writeOptions = new WriteOptions();
            writeOptions.DisableWal(0);
            writeOptions.SetSync(true);
            _rocksDb.Remove(key, null, writeOptions);
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