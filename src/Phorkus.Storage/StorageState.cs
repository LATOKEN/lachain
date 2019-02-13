using System.Collections.Generic;
using Phorkus.Storage.Trie;

namespace Phorkus.Storage
{
    internal class StorageState : IStorageState
    {
        private readonly RepositoryManager _repositoryManager;
        private readonly ITrieMap _trieMap;
        
        private readonly ulong _initialVersion;

        // This constructor is internal and only used in RepositoryManager
        // Constructing such an object manually can result in error in commit phase
        internal StorageState(RepositoryManager repositoryManager)
        {
            _repositoryManager = repositoryManager;
            _trieMap = repositoryManager.TrieMap;
            CurrentVersion = repositoryManager.LatestVersion;
            _initialVersion = repositoryManager.LatestVersion;
        }

        internal StorageState(RepositoryManager repositoryManager, ulong version)
        {
            _repositoryManager = repositoryManager;
            _trieMap = repositoryManager.TrieMap;
            CurrentVersion = version;
            _initialVersion = repositoryManager.LatestVersion;
        }

        public ulong CurrentVersion { get; private set; }

        public byte[] Get(byte[] key)
        {
            return _trieMap.Find(CurrentVersion, key);
        }

        public ulong Add(byte[] key, byte[] value)
        {
            CurrentVersion = _trieMap.Add(CurrentVersion, key, value);
            return CurrentVersion;
        }

        public ulong AddOrUpdate(byte[] key, byte[] value)
        {
            CurrentVersion = _trieMap.AddOrUpdate(CurrentVersion, key, value);
            return CurrentVersion;
        }

        public ulong Update(byte[] key, byte[] value)
        {
            CurrentVersion = _trieMap.Update(CurrentVersion, key, value);
            return CurrentVersion;
        }

        public ulong Delete(byte[] key, out byte[] value)
        {
            CurrentVersion = _trieMap.Delete(CurrentVersion, key, out value);
            return CurrentVersion;
        }

        public ulong TryDelete(byte[] key, out byte[] value)
        {
            CurrentVersion = _trieMap.TryDelete(CurrentVersion, key, out value);
            return CurrentVersion;
        }

        public IEnumerable<byte[]> Values => _trieMap.GetValues(CurrentVersion);

        public ulong Commit()
        {
            _trieMap.Checkpoint(CurrentVersion);
            _repositoryManager.SetState(CurrentVersion);
            return CurrentVersion;
        }

        public ulong Cancel()
        {
            _trieMap.ClearCaches();
            _repositoryManager.SetState(_initialVersion);
            return _initialVersion;
        }
    }
}