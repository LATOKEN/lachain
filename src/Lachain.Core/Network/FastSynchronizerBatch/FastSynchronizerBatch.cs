/*
    This file controls the fast_sync, other necessary classes are instantiated here.
    We will be downloading 6 tree data structures, one at a time. Block headers will be downloaded differently, not as a tree.    
*/
using System;
using System.Collections.Generic;
using System.Linq;
using Lachain.Logger;
using Lachain.Networking;
using Lachain.Proto;
using Lachain.Storage;
using Lachain.Storage.Repositories;
using Lachain.Storage.State;
using Lachain.Storage.Trie;
using Lachain.Utility.Utils;
using Lachain.Utility.Serialization;
using Google.Protobuf;

namespace Lachain.Core.Network.FastSynchronizerBatch
{
    
    public class FastSynchronizerBatch : IFastSynchronizerBatch
    {
        private string[] trieNames = new string[]
        {
                "Balances", "Contracts", "Storage", "Transactions", "Events", "Validators"
        };
        private static readonly ILogger<FastSynchronizer> Logger = LoggerFactory.GetLoggerForClass<FastSynchronizer>();
        private readonly IStateManager _stateManager;
        private readonly IRocksDbContext _dbContext;
        private readonly ISnapshotIndexRepository _snapshotIndexRepository;
        private readonly INetworkManager _networkManager;
        private readonly VersionFactory _versionFactory;
        private readonly IFastSyncRepository _repository;
        private readonly IHybridQueue _hybridQueue;
        private readonly IRequestManager _requestManager;
        private readonly IDownloader _downloader;
        private readonly PeerManager _peerManager;
        public FastSynchronizerBatch(
            IStateManager stateManager,
            IRocksDbContext dbContext,
            ISnapshotIndexRepository snapshotIndexRepository,
            INetworkManager networkManager,
            IStorageManager storageManager,
            IFastSyncRepository repository,
            IHybridQueue hybridQueue,
            IRequestManager requestManager,
            IDownloader downloader
        )
        {
            _stateManager = stateManager;
            _dbContext = dbContext;
            _snapshotIndexRepository = snapshotIndexRepository;
            _networkManager = networkManager;
            _repository = repository;
            _hybridQueue = hybridQueue;
            _requestManager = requestManager;
            _downloader = downloader;
            _versionFactory = storageManager.GetVersionFactory();
            _peerManager = _downloader.GetPeerManager();
        }

        //Fast_sync is started from this function.
        //urls is the list of peer nodes, we'll be requesting for data throughtout this process
        //blockNumber denotes which block we want to sync with, if it is 0, we will ask for the latest block number to a random peer and
        //start synching with that peer
        public void StartSync(ulong blockNumber, UInt256 blockHash, List<(UInt256, CheckpointType)> stateHashes)
        {
            //At first we check if fast sync have started and completed before.
            // If it has completed previously, we don't let the user run it again.
            if(Alldone())
            {
                Console.WriteLine("Fast Sync was done previously\nReturning");
                return;
            }
            
            Console.WriteLine("Current Version: " + _versionFactory.CurrentVersion);

            //If fast_sync was started previously, then this variable should contain which block number we are trying to sync with, otherwise 0.
            //If it is non-zero, then we will forcefully sync with that block irrespective of what the user input for blockNumber is now.
            ulong savedBlockNumber = _repository.GetBlockNumber();
            var savedSateHashes = new List<(UInt256, CheckpointType)>();
            if(savedBlockNumber != 0)
            {
                blockNumber = savedBlockNumber;
                blockHash = _repository.GetBlockHash()!;
                foreach (var checkpointInfo in stateHashes)
                {
                    var (stateHash, checkpointType) = checkpointInfo;
                    stateHash = _repository.GetStateHash(checkpointType);
                    if (stateHash is null)
                        throw new ArgumentException($"Got null hash for checkpoint type: {checkpointType}");
                    savedSateHashes.Add((stateHash!, checkpointType));
                }
                stateHashes = savedSateHashes;
            }
            if (stateHashes.Count != 6)
                throw new ArgumentException($"There must be six state-hash, got {stateHashes.Count}");

            // Checking if we have root hashes for all six tries.
            foreach (var trieName in trieNames)
            {
                CheckRootHashExist(trieName, stateHashes);
            }
            
            if (savedBlockNumber == 0) _repository.Initialize(blockNumber, blockHash, stateHashes);
            //to keep track how many tries have been downloaded till now, saved in db with LastDownloadedTries prefix
            int downloadedTries = _repository.GetLastDownloadedTries();
            _hybridQueue.Initialize();

            for(int i = downloadedTries; i < trieNames.Length; i++)
            {
                Logger.LogTrace($"Starting trie {trieNames[i]}");
                var rootHash = GetRootHashForTrieName(trieNames[i], stateHashes);
                _downloader.GetTrie(rootHash);
                bool foundRoot = _repository.GetIdByHash(rootHash, out ulong curTrieRoot);
            //    snapshots[i].SetCurrentVersion(curTrieRoot);
                downloadedTries++;
                _dbContext.Save(EntryPrefix.LastDownloadedTries.BuildPrefix(), downloadedTries.ToBytes().ToArray());
                Logger.LogTrace($"Ending trie {trieNames[i]} : {curTrieRoot}");
            //    bool isConsistent = requestManager.CheckConsistency(curTrieRoot);
            //    Console.WriteLine("Is Consistent : "+isConsistent );
                Logger.LogTrace($"Total Nodes downloaded: {_versionFactory.CurrentVersion}");
            }
            
            if(downloadedTries==(int)trieNames.Length)
            {
                var blockchainSnapshot = _stateManager.NewSnapshot();
                _downloader.DownloadBlocks();
                foreach (var trieName in trieNames)
                {
                    var rootHash = GetRootHashForTrieName(trieName, stateHashes);
                    bool foundHash = _repository.GetIdByHash(rootHash, out ulong trieRoot);
                    var snapshot = blockchainSnapshot.GetSnapshot(trieName);
                    snapshot!.SetCurrentVersion(trieRoot);
                }

                _stateManager.Approve();
                _stateManager.Commit();
                _snapshotIndexRepository.SaveSnapshotForBlock(blockNumber, blockchainSnapshot);
                
                downloadedTries++;
                _repository.SetLastDownloadedTries(downloadedTries);
                
                Logger.LogWarning($"Set state to block {blockNumber} complete");
            }
        }

        private void CheckRootHashExist(string trieName, List<(UInt256, CheckpointType)> stateHashes)
        {
            var checkpointType = CheckpointUtils.GetCheckpointTypeForSnapshotName(trieName);
            foreach (var (stateHash, _checkpointType) in stateHashes)
            {
                if (checkpointType == _checkpointType) return;
            }
            throw new Exception($"Root hash for {trieName} not found in stateHashes");
        }

        private UInt256 GetRootHashForTrieName(string trieName, List<(UInt256, CheckpointType)> stateHashes)
        {
            var checkpointType = CheckpointUtils.GetCheckpointTypeForSnapshotName(trieName);
            UInt256? stateHash = null;
            foreach (var (_stateHash, _checkpointType) in stateHashes)
            {
                if (checkpointType == _checkpointType)
                {
                    stateHash = _stateHash;
                    break;
                }
            }
            return stateHash!;
        }

        private bool Alldone()
        {
            var tiresDownloaded = _repository.GetLastDownloadedTries();
            return tiresDownloaded == (trieNames.Length + 1) ;
        }
    }
}
