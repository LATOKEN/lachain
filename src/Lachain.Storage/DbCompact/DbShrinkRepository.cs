using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Lachain.Storage.DbCompact
{
    public class DbShrinkRepository : IDbShrinkRepository
    {
        private IRocksDbContext _dbContext;
        private RocksDbAtomicWrite batch;
        private ConcurrentDictionary<byte[], byte[]> _memDb;
        private ISet<byte[]> _toDelete;

        public DbShrinkRepository(IRocksDbContext dbContext)
        {
            _dbContext = dbContext;
            UpdateBatch();
        }

        private void UpdateBatch()
        {
            batch = new RocksDbAtomicWrite(_dbContext);
            _memDb = new ConcurrentDictionary<byte[], byte[]>();
            _toDelete = new HashSet<byte[]>();
        }

        public void Save(byte[] key, byte[] content, bool tryCommit = true)
        {
            batch.Put(key, content);
            _toDelete.Remove(key);
            _memDb[key] = content;
            DbShrinkUtils.UpdateCounter();
            if(tryCommit && DbShrinkUtils.CycleEnded())
            {
                Commit();
            }
        }

        public void Delete(byte[] key, bool tryCommit = true)
        {
            batch.Delete(key);
            _memDb.TryRemove(key,out var _);
            _toDelete.Add(key);
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
            if (_memDb.ContainsKey(key)) return _memDb[key];
            if (_toDelete.Contains(key)) return null;
            return _dbContext.Get(key);
        }

        public bool KeyExists(byte[] key)
        {
            if (_memDb.ContainsKey(key)) return true;
            if (_toDelete.Contains(key)) return false;
            var content = _dbContext.Get(key);
            if (content is null) return false;
            return true;
        }

    }
}