using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Phorkus.Storage.Repositories;
using Phorkus.Storage.RocksDB;

namespace Phorkus.Storage
{
    public class PersistentStorageManager : IPersistentStorageManager
    {
        private readonly IDictionary<uint, RepositoryManager> _repositoryManagers =
            new ConcurrentDictionary<uint, RepositoryManager>();
        
        public PersistentStorageManager(IRocksDbContext rocksDbContext)
        {
            var versionIndexer = new VersionIndexer(rocksDbContext);
            var versionFactory = new VersionFactory(versionIndexer.GetVersion(0));
            foreach (var repository in Enum.GetValues(typeof(RepositoryType)).Cast<RepositoryType>())
            {
                _repositoryManagers[(uint) repository] = new RepositoryManager(
                    (uint) repository, rocksDbContext, versionFactory, versionIndexer
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

        public IStorageState GetLastState(uint repository)
        {
            return _repositoryManagers[repository].GetLastState();
        }

        public IStorageState GetState(uint repository, ulong version)
        {
            return _repositoryManagers[repository].GetState(version);
        }

        public void SetLastState(uint repositrory, IStorageState state)
        {
            _repositoryManagers[repositrory].SetState(state.CurrentVersion);
        }
    }
}