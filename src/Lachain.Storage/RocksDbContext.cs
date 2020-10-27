using System;
using System.Collections.Generic;
using System.Linq;
using RocksDbSharp;

namespace Lachain.Storage
{
    public class RocksDbContext : IRocksDbContext
    {
        private readonly RocksDb _rocksDb;
        private readonly WriteOptions _writeOptions;


        public RocksDbContext(string path = "ChainLachain")
        {
            _writeOptions = new WriteOptions();
            _writeOptions.DisableWal(0);
            _writeOptions.SetSync(true);

            var options = new DbOptions().SetCreateIfMissing();
            _rocksDb = RocksDb.Open(options, path);
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
            _rocksDb.Put(key, content, null, _writeOptions);
        }

        public void SaveBatch(WriteBatch batch)
        {
            _ThrowIfNotInitialized();
            _rocksDb.Write(batch, _writeOptions);
        }
        
        public void Save(IEnumerable<byte> key, IEnumerable<byte> content)
        {
            _ThrowIfNotInitialized();
            _rocksDb.Put(key.ToArray(), content.ToArray(), null, _writeOptions);
        }

        public void Delete(byte[] key)
        {
            _ThrowIfNotInitialized();
            _rocksDb.Remove(key, null, _writeOptions);
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