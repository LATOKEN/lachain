using System.Collections.Generic;

namespace Phorkus.Hestia
{
    class StorageState : IStorageState
    {
        private readonly RepositoryManager _repositoryManager;
        private readonly IMapManager _mapManager;
        private readonly ulong _initialVersion;

        // This constructor is internal and only used in RepositoryManager
        // Constructing such an object manually can result in error in commit phase
        internal StorageState(RepositoryManager repositoryManager)
        {
            _repositoryManager = repositoryManager;
            _mapManager = repositoryManager.MapManager;
            CurrentVersion = repositoryManager.LatestVersion;
            _initialVersion = repositoryManager.LatestVersion;
        }

        internal StorageState(RepositoryManager repositoryManager, ulong version)
        {
            _repositoryManager = repositoryManager;
            _mapManager = repositoryManager.MapManager;
            CurrentVersion = version;
            _initialVersion = repositoryManager.LatestVersion;
        }

        public ulong CurrentVersion { get; private set; }

        public byte[] Get(byte[] key)
        {
            return _mapManager.Find(CurrentVersion, key);
        }

        public ulong Add(byte[] key, byte[] value)
        {
            CurrentVersion = _mapManager.Add(CurrentVersion, key, value);
            return CurrentVersion;
        }

        public ulong AddOrUpdate(byte[] key, byte[] value)
        {
            CurrentVersion = _mapManager.AddOrUpdate(CurrentVersion, key, value);
            return CurrentVersion;
        }

        public ulong Update(byte[] key, byte[] value)
        {
            CurrentVersion = _mapManager.Update(CurrentVersion, key, value);
            return CurrentVersion;
        }

        public ulong Delete(byte[] key, out byte[] value)
        {
            CurrentVersion = _mapManager.Delete(CurrentVersion, key, out value);
            return CurrentVersion;
        }

        public ulong TryDelete(byte[] key, out byte[] value)
        {
            CurrentVersion = _mapManager.TryDelete(CurrentVersion, key, out value);
            return CurrentVersion;
        }

        public IEnumerable<byte[]> Keys => _mapManager.GetKeys(CurrentVersion);
        public IEnumerable<byte[]> Values => _mapManager.GetValues(CurrentVersion);
        public IEnumerable<KeyValuePair<byte[], byte[]>> Entries => _mapManager.GetEntries(CurrentVersion);

        public ulong Commit()
        {
            _mapManager.Checkpoint(CurrentVersion);
            _repositoryManager.SetState(CurrentVersion);
            return CurrentVersion;
        }

        public ulong Cancel()
        {
            _mapManager.ClearCaches();
            _repositoryManager.SetState(_initialVersion);
            return _initialVersion;
        }
    }
}