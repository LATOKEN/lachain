using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Lachain.Utility.Utils;
using RocksDbSharp;
using Lachain.Logger;

namespace Lachain.Storage
{
    public class RocksDbContext : IRocksDbContext
    {
        private static readonly ILogger<RocksDbContext> Logger = LoggerFactory.GetLoggerForClass<RocksDbContext>();

        private readonly RocksDb _rocksDb;
        private readonly WriteOptions _writeOptions;
        private readonly string _dbpath;

        public RocksDbContext(string path = "ChainLachain")
        {
            _dbpath = path;
            _writeOptions = new WriteOptions();
            _writeOptions.DisableWal(0);
            _writeOptions.SetSync(true);

            var options = new DbOptions().SetCreateIfMissing();
            _rocksDb = RocksDb.Open(options, _dbpath);
        }

        public ulong EstimateNumberOfKeys()
        {
            return ulong.Parse(_rocksDb.GetProperty("rocksdb.estimate-num-keys"));
        }

        public ulong EstimateDirSize()
        {
            return (ulong) DirUtils.DirSize(new DirectoryInfo(_dbpath));
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

        public void CompactAll()
        {
            Logger.LogInformation($"Starting Compaction.");
            _rocksDb.CompactRange((byte[]?)null, (byte[]?)null);
            Logger.LogInformation($"Ended Compaction");
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