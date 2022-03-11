using System.Collections.Concurrent;
using System.Collections.Generic;
using Lachain.Proto;
using Lachain.Crypto;

namespace Lachain.Storage.DbCompact
{
    public class DbShrinkRepository : IDbShrinkRepository
    {
        private IRocksDbContext _dbContext;
        private RocksDbAtomicWrite batch;
        private readonly ConcurrentDictionary<UInt256, byte[]> _memDb = new ConcurrentDictionary<UInt256, byte[]>();
        private ISet<UInt256> _toDelete = new HashSet<UInt256>();

        public DbShrinkRepository(IRocksDbContext dbContext)
        {
            _dbContext = dbContext;
            UpdateBatch();
        }

        private void UpdateBatch()
        {
            batch = new RocksDbAtomicWrite(_dbContext);
            _memDb.Clear();
            _toDelete.Clear();
        }

        public void Save(byte[] key, byte[] content, bool tryCommit = true)
        {
            batch.Put(key, content);
            var keyHash = key.Keccak();
            _toDelete.Remove(keyHash);
            _memDb[keyHash] = content;
            DbShrinkUtils.UpdateCounter();
            if(tryCommit && DbShrinkUtils.CycleEnded())
            {
                Commit();
            }
        }

        public void Delete(byte[] key, bool tryCommit = true)
        {
            batch.Delete(key);
            var keyHash = key.Keccak();
            _memDb.TryRemove(keyHash,out var _);
            _toDelete.Add(keyHash);
            DbShrinkUtils.UpdateCounter();
            if(tryCommit && DbShrinkUtils.CycleEnded())
            {
                Commit();
            }
        }

        public void Commit()
        {
            batch.Commit();
            DbShrinkUtils.ResetCounter();
            UpdateBatch();
        }

        public byte[]? Get(byte[] key)
        {
            var keyHash = key.Keccak();
            if (_memDb.ContainsKey(keyHash)) return _memDb[keyHash];
            if (_toDelete.Contains(keyHash)) return null;
            return _dbContext.Get(key);
        }

        public bool KeyExists(byte[] key)
        {
            var keyHash = key.Keccak();
            if (_memDb.ContainsKey(keyHash)) return true;
            if (_toDelete.Contains(keyHash)) return false;
            var content = _dbContext.Get(key);
            if (content is null) return false;
            return true;
        }

    }
}