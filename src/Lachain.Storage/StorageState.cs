using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Lachain.Proto;
using Lachain.Storage.Trie;
using Lachain.Utility.Utils;
using RocksDbSharp;

namespace Lachain.Storage
{
    public class StorageState : IStorageState
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

        public ulong CurrentVersion { get; set; }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public byte[]? Get(byte[] key)
        {
            return _trieMap.Find(CurrentVersion, key);
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public ulong Add(byte[] key, byte[] value)
        {
            CurrentVersion = _trieMap.Add(CurrentVersion, key, value);
            return CurrentVersion;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public ulong AddOrUpdate(byte[] key, byte[] value)
        {
            CurrentVersion = _trieMap.AddOrUpdate(CurrentVersion, key, value);
            Console.WriteLine($"CurrentVersion = {CurrentVersion}");
            return CurrentVersion;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public ulong Update(byte[] key, byte[] value)
        {
            CurrentVersion = _trieMap.Update(CurrentVersion, key, value);
            return CurrentVersion;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public ulong Delete(byte[] key, out byte[]? value)
        {
            CurrentVersion = _trieMap.Delete(CurrentVersion, key, out value);
            return CurrentVersion;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public ulong TryDelete(byte[] key, out byte[]? value)
        {
            CurrentVersion = _trieMap.TryDelete(CurrentVersion, key, out value);
            return CurrentVersion;
        }

        public IEnumerable<byte[]> Values => _trieMap.GetValues(CurrentVersion);

        public UInt256 Hash => _trieMap.GetHash(CurrentVersion);

        [MethodImpl(MethodImplOptions.Synchronized)]
        public ulong Commit()
        {
            using var tx = _repositoryManager.CreateTransaction();
            _trieMap.Checkpoint(CurrentVersion, tx);
            _repositoryManager.SetState(CurrentVersion, tx);
            tx.Commit();
            return CurrentVersion;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public ulong Cancel()
        {
            _trieMap.ClearCaches();
            return _initialVersion;
        }
    }
}