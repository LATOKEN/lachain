using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Lachain.Storage.Repositories;
using Lachain.Storage.State;

namespace Lachain.Storage
{
    public class StorageManager : IStorageManager
    {
        private readonly IDictionary<uint, RepositoryManager> _repositoryManagers =
            new ConcurrentDictionary<uint, RepositoryManager>();
        private VersionFactory _versionFactory;
        public StorageManager(IRocksDbContext rocksDbContext)
        {
            var versionIndexer = new VersionRepository(rocksDbContext);
            _versionFactory = new VersionFactory(versionIndexer.GetVersion(0));
            foreach (var repository in Enum.GetValues(typeof(RepositoryType)).Cast<RepositoryType>())
            {
                _repositoryManagers[(uint) repository] = new RepositoryManager(
                    (uint) repository, rocksDbContext, _versionFactory, versionIndexer
                );
            }
        }
        
        [MethodImpl(MethodImplOptions.Synchronized)]
        public ulong LatestCommitedVersion(uint repository)
        {
            return _repositoryManagers[repository].LatestVersion;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public byte[]? Get(uint repository, ulong version, byte[] key)
        {
            return _repositoryManagers[repository].TrieMap.Find(version, key);
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public IStorageState GetLastState(uint repository)
        {
            return _repositoryManagers[repository].GetLastState();
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public IStorageState GetState(uint repository, ulong version)
        {
            return _repositoryManagers[repository].GetState(version);
        }

        public VersionFactory GetVersionFactory()
        {
            return _versionFactory;
        }
    }
}