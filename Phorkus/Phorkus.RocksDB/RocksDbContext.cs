using System;
using System.Collections.Generic;
using System.Linq;

namespace Phorkus.RocksDB
{
    public class RocksDbContext : IRocksDbContext
    {
        private readonly RocksDb _rocksDb;

        public RocksDbContext(RocksDbConfig config)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));

            // Initialize RocksDB (Connection String is the path to use)
            var options = new DbOptions().SetCreateIfMissing();
            // TODO #358: please avoid sync IO in constructor -> Open connection with the first operation for now
            _rocksDb = RocksDb.Open(options, config.FilePath);
        }

        public byte[] Get(byte[] key)
        {
            return _rocksDb.Get(key);
        }

        public IDictionary<byte[], byte[]> GetMany(IEnumerable<byte[]> keys)
        {
            return _rocksDb.MultiGet(keys.ToArray()).ToDictionary(kv => kv.Key, k => k.Value);
        }

        public void Save(byte[] key, byte[] content)
        {
            _rocksDb.Put(key, content);
        }
        
        public void Save(IEnumerable<byte> key, IEnumerable<byte> content)
        {
            /* TODO: "this conversion might be optimized via unsafe mode I think" */
            _rocksDb.Put(key.ToArray(), content.ToArray());
        }

        public void Delete(byte[] key)
        {
            _rocksDb.Remove(key);
        }
        
        public void Dispose()
        {
            _rocksDb?.Dispose();
        }
    }
}