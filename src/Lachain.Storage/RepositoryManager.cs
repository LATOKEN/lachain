using Lachain.Storage.Repositories;
using Lachain.Storage.State;
using Lachain.Storage.Trie;

namespace Lachain.Storage
{
    /*
        There are seven tries in the storage and there is one repository manager for each trie. 
        _repositryId represents which trie it manages. There is also an additional meta repository manager. 
    */
    public class RepositoryManager
    {
        private readonly uint _repositoryId;
        private readonly IRocksDbContext _dbContext;
        private readonly VersionFactory _versionFactory;
        private readonly VersionRepository _versionRepository;
        public readonly ITrieMap TrieMap;

        public uint RepositoryId => _repositoryId;
        public ulong LatestVersion { get; private set; }
        
        public RepositoryManager(
            uint repositoryId,
            IRocksDbContext dbContext,
            VersionFactory versionFactory,
            VersionRepository versionRepository
            )
        {
            _repositoryId = repositoryId;
            _dbContext = dbContext;
            _versionFactory = versionFactory;
            _versionRepository = versionRepository;
            var storageContext = new NodeRepository(dbContext);
            TrieMap = new TrieHashMap(storageContext, versionFactory);
            LatestVersion = _versionRepository.GetVersion(_repositoryId);
        }

        public RocksDbAtomicWrite CreateTransaction()
        {
            return new RocksDbAtomicWrite(_dbContext);
        }

        public IStorageState GetLastState()
        {
            return new StorageState(this);
        }

        public IStorageState GetState(ulong version)
        {
            return new StorageState(this, version);
        }

        internal void SetState(ulong version, RocksDbAtomicWrite tx)
        {
            LatestVersion = version;
            _versionRepository.SetVersion(_repositoryId, LatestVersion, tx);
            _versionRepository.SetVersion((uint) RepositoryType.MetaRepository, _versionFactory.CurrentVersion + 1, tx);
        }
    }
}