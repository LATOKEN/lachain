using System;
using Phorkus.Hestia.PersistentHashTrie;
using Phorkus.Hestia.PersistentMap;
using Phorkus.RocksDB;

namespace Phorkus.Hestia
{
    public class RepositoryManager
    {
        private readonly uint _repositoryId;
        private readonly VersionFactory _versionFactory;
        private readonly VersionIndexer _versionIndexer;
        public readonly IMapManager MapManager;
        public ulong LatestVersion { get; private set; }
        public bool TransactionInProgress { get; private set; }

        public RepositoryManager(
            uint repositoryId, IRocksDbContext dbContext,
            VersionFactory versionFactory, VersionIndexer versionIndexer
        )
        {
            _repositoryId = repositoryId;
            _versionFactory = versionFactory;
            _versionIndexer = versionIndexer;
            var storageContext = new PersistentHashTrieStorageContext(dbContext);
            MapManager = new PersistentHashTrieManager(storageContext, versionFactory);
            LatestVersion = _versionIndexer.GetVersion(_repositoryId);
            TransactionInProgress = false;
        }

        public IStorageState NewState()
        {
            if (TransactionInProgress)
                throw new InvalidOperationException(
                    "There are pending changes already: either commit or rollback them"
                );
            TransactionInProgress = true;
            return new StorageState(this);
        }

        internal void TerminateTransaction(ulong version)
        {
            LatestVersion = version;
            _versionIndexer.SetVersion(_repositoryId, LatestVersion);
            _versionIndexer.SetVersion(0, _versionFactory.CurrentVersion + 1);
            TransactionInProgress = false;
        }
    }
}