using System.Collections.Concurrent;
using System.Collections.Generic;
using Phorkus.Hestia.PersistentMap;
using Phorkus.RocksDB;

namespace Phorkus.Hestia
{
    public class StorageManager : IStorageManager
    {
        private readonly IDictionary<uint, RepositoryManager> _repositoryManagers =
            new ConcurrentDictionary<uint, RepositoryManager>();

        public StorageManager(IRocksDbContext rocksDbContext, IEnumerable<uint> repositories)
        {
            var dbContext = rocksDbContext;
            var versionIndexer = new VersionIndexer(dbContext);
            var versionFactory = new VersionFactory(versionIndexer.GetVersion(0));
            foreach (var repository in repositories)
            {
                _repositoryManagers[repository] = new RepositoryManager(
                    repository, dbContext, versionFactory, versionIndexer
                );
            }
        }

        public ulong LatestCommitedVersion(uint repository)
        {
            return _repositoryManagers[repository].LatestVersion;
        }

        public byte[] Get(uint repository, ulong version, byte[] key)
        {
            return _repositoryManagers[repository].MapManager.Find(version, key);
        }

        public IStorageState NewState(uint repository)
        {
            return _repositoryManagers[repository].NewState();
        }
    }
}