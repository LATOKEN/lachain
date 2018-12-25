using System;
using Phorkus.Storage.RocksDB;

namespace Phorkus.Storage
{
    public class VersionIndexer
    {
        private readonly IRocksDbContext _dbContext;

        public VersionIndexer(IRocksDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public ulong GetVersion(uint repository)
        {
            var rawVersion = _dbContext.Get(EntryPrefix.StorageVersionIndex.BuildPrefix(repository));
            return rawVersion != null ? BitConverter.ToUInt64(rawVersion, 0) : 0u;
        }

        public void SetVersion(uint repository, ulong version)
        {
            _dbContext.Save(EntryPrefix.StorageVersionIndex.BuildPrefix(repository), BitConverter.GetBytes(version));
        }
    }
}