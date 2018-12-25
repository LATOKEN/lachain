using Phorkus.Storage.PersistentHashTrie;
using Phorkus.Storage.Repositories;
using Phorkus.Storage.RocksDB;

namespace Phorkus.Storage
{
    public class RepositoryManager
    {
        private readonly uint _repositoryId;
        private readonly VersionFactory _versionFactory;
        private readonly VersionIndexer _versionIndexer;
        public readonly IMapManager MapManager;
        public ulong LatestVersion { get; private set; }
        
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
        }

        public IStorageState GetLastState()
        {
            return new StorageState(this);
        }

        public IStorageState GetState(ulong version)
        {
            return new StorageState(this, version);
        }

        internal void SetState(ulong version)
        {
            LatestVersion = version;
            _versionIndexer.SetVersion(_repositoryId, LatestVersion);
            _versionIndexer.SetVersion((uint) RepositoryType.MetadataRepository, _versionFactory.CurrentVersion + 1);
        }
    }
}