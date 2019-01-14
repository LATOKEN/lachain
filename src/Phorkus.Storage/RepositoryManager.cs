using Phorkus.Storage.Repositories;
using Phorkus.Storage.State;
using Phorkus.Storage.Trie;

namespace Phorkus.Storage
{
    public class RepositoryManager
    {
        private readonly uint _repositoryId;
        private readonly VersionFactory _versionFactory;
        private readonly VersionRepository _versionRepository;
        public readonly ITrieMap TrieMap;
        public ulong LatestVersion { get; private set; }
        
        public RepositoryManager(
            uint repositoryId,
            IRocksDbContext dbContext,
            VersionFactory versionFactory,
            VersionRepository versionRepository)
        {
            _repositoryId = repositoryId;
            _versionFactory = versionFactory;
            _versionRepository = versionRepository;
            var storageContext = new NodeRepository(dbContext);
            TrieMap = new TrieHashMap(storageContext, versionFactory);
            LatestVersion = _versionRepository.GetVersion(_repositoryId);
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
            _versionRepository.SetVersion(_repositoryId, LatestVersion);
            _versionRepository.SetVersion((uint) RepositoryType.MetaRepository, _versionFactory.CurrentVersion + 1);
        }
    }
}