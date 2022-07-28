/*
    This file controls the fast_sync, other necessary classes are instantiated here.
    We will be downloading 6 tree data structures, one at a time. Block headers will be downloaded differently, not as a tree.    

    **SCOPE TO IMPROVE**:   Blocks are downloaded one by one. This creates lots of unnecessary nodes which can be deleted after
                            set state is complete.

    **FEATURE TO ADD**:     Fastsync is possible only for the first time. If fastsync is done before, fastsync is not allowed.
                            It can be studied if it is necessary and add features to allow fastsync anytime.
*/
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Lachain.Logger;
using Lachain.Proto;
using Lachain.Storage;
using Lachain.Utility.Utils;
using Lachain.Utility.Serialization;


namespace Lachain.Core.Network.FastSynchronizerBatch
{
    
    public class FastSynchronizerBatch : IFastSynchronizerBatch
    {
        private string[] trieNames = new string[]
        {
                "Balances", "Contracts", "Storage", "Transactions", "Events", "Validators"
        };
        private static readonly ILogger<FastSynchronizer> Logger = LoggerFactory.GetLoggerForClass<FastSynchronizer>();
        private readonly VersionFactory _versionFactory;
        private readonly IFastSyncRepository _repository;
        private readonly IHybridQueue _hybridQueue;
        private readonly IDownloader _downloader;
        private readonly IBlockRequestManager _blockRequestManager;
        private readonly PeerManager _peerManager;
        // This will is the difference of block height between nodes when fast sync is needed
        // If the difference is less than this the fast sync will not be triggered
        public static readonly ulong FastSyncBlockDiff = 1000000;
        public FastSynchronizerBatch(
            IFastSyncRepository repository,
            IHybridQueue hybridQueue,
            IDownloader downloader,
            IBlockRequestManager blockRequestManager
        )
        {
            _repository = repository;
            _hybridQueue = hybridQueue;
            _downloader = downloader;
            _blockRequestManager = blockRequestManager;
            _versionFactory = _repository.GetVersionFactory();
            _peerManager = _downloader.GetPeerManager();
        }

        // Fast_sync is started from this function.
        // urls is the list of peer nodes, we'll be requesting for data throughtout this process
        // blockNumber denotes which block we want to sync with, if it is 0, we will ask for the latest block number to a random peer and
        // start synching with that peer
        public void StartSync(ulong? blockNumber, UInt256? blockHash, List<(UInt256, CheckpointType)>? stateHashes)
        {
            // At first we check if fast sync have started and completed before.
            // If it has completed previously, we don't let the user run it again.
            if(Alldone())
            {
                Logger.LogTrace("Fast Sync was done previously\nReturning");
                return;
            }

            // If fast_sync was started previously, then this variable should contain which block number we are trying to sync with, otherwise 0.
            // If it is non-zero, then we will forcefully sync with that block irrespective of what the user input for blockNumber is now.
            ulong savedBlockNumber = _repository.GetCheckpointBlockNumber();
            var savedSateHashes = new List<(UInt256, CheckpointType)>();
            if (savedBlockNumber != 0)
            {
                blockNumber = savedBlockNumber;
                blockHash = _repository.GetCheckpointBlockHash()!;
                foreach (var trieName in trieNames)
                {
                    var checkpointType = CheckpointUtils.GetCheckpointTypeForSnapshotName(trieName);
                    if (checkpointType is null)
                        throw new Exception($"trie name {trieName} is not correct");
                    var stateHash = _repository.GetCheckpointStateHash(checkpointType.Value);
                    if (stateHash is null)
                        throw new ArgumentException($"Got null hash for checkpoint type: {checkpointType}");
                    savedSateHashes.Add((stateHash!, checkpointType.Value));
                }
                stateHashes = savedSateHashes;
            }
            if (stateHashes!.Count != 6)
                throw new ArgumentException($"There must be six state-hash, got {stateHashes.Count}");

            // Checking if we have root hashes for all six tries.
            foreach (var trieName in trieNames)
            {
                CheckRootHashExist(trieName, stateHashes);
            }
            
            if (savedBlockNumber == 0) _repository.Initialize(blockNumber!.Value, blockHash!, stateHashes);
            Logger.LogTrace($"Starting fast sync with checkpoint block {blockNumber!.Value}");
            Logger.LogTrace("Current Version: " + _versionFactory.CurrentVersion);
            // to keep track how many tries have been downloaded till now, saved in db with LastDownloadedTries prefix
            int downloadedTries = _repository.GetLastDownloadedTries();
            _hybridQueue.Initialize();

            for(int i = downloadedTries; i < trieNames.Length; i++)
            {
                Logger.LogTrace($"Starting trie {trieNames[i]}");
                UInt256 rootHash = GetRootHashForTrieName(trieNames[i], stateHashes)!;
                _downloader.GetTrie(rootHash);
                bool foundRoot = _repository.GetIdByHash(rootHash, out ulong curTrieRoot);
                downloadedTries++;
                _repository.SetLastDownloadedTries(downloadedTries);
                Logger.LogTrace($"Ending trie {trieNames[i]} : {curTrieRoot}");
                Logger.LogTrace($"Total Nodes downloaded: {_versionFactory.CurrentVersion}");
            }
            
            if (downloadedTries == trieNames.Length)
            {
                _blockRequestManager.Initialize();
                _downloader.DownloadBlocks();
                foreach (var trieName in trieNames)
                {
                    UInt256 rootHash = GetRootHashForTrieName(trieName, stateHashes)!;
                    _repository.SetSnapshotVersion(trieName, rootHash);
                }

                _repository.SetState();
                downloadedTries++;
                _repository.SetLastDownloadedTries(downloadedTries);
                
                Logger.LogTrace($"Set state to block {blockNumber} complete");
            }
        }

        public void AddPeer(ECDSAPublicKey publicKey)
        {
            _peerManager.AddPeer(publicKey);
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

        private UInt256? GetRootHashForTrieName(string trieName, List<(UInt256, CheckpointType)> stateHashes)
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
            return stateHash;
        }

        private bool Alldone()
        {
            var tiresDownloaded = _repository.GetLastDownloadedTries();
            return tiresDownloaded == (trieNames.Length + 1) ;
        }

        public bool IsRunning()
        {
            return _repository.GetCheckpointBlockNumber() > 0;
        }

        private bool MatchStateHash(
            UInt256 expectedStateHash,
            CheckpointType checkpointType,
            List<(UInt256, CheckpointType)> stateHashes
        )
        {
            foreach (var (stateHash, checkpoint) in stateHashes)
            {
                if (checkpointType == checkpoint)
                {
                    return stateHash.Equals(expectedStateHash);
                }
            }
            Logger.LogWarning($"Could not find {checkpointType} in stateHashes");
            return false;
        }

        public bool IsCheckpointOk(ulong? blockHeight, UInt256? blockHash, List<(UInt256, CheckpointType)>? stateHashes)
        {
            Logger.LogTrace("Verifying checkpoint information...");
            if (blockHash is null || blockHeight is null || stateHashes is null || stateHashes.Count != 6)
            {
                Logger.LogTrace("Checkpoint information missing");
                return false;
            }
            // Checking if we have root hashes for all six tries.
            try
            {
                foreach (var trieName in trieNames)
                {
                    CheckRootHashExist(trieName, stateHashes);
                }
            }
            catch (Exception exception)
            {
                Logger.LogWarning($"Got exception while checking root hash: {exception}");
                return false;
            }

            Task.Factory.StartNew(() =>
            {
                _downloader.DownloadCheckpoint(blockHeight.Value, trieNames);
            }, TaskCreationOptions.LongRunning);

            while (_downloader.CheckpointBlockHash is null)
            {
                Thread.Sleep(1000);
            }
            if (!_downloader.CheckpointBlockHash.Equals(blockHash))
            {
                Logger.LogTrace("Checkpoint block hash mismatch");
                return false;
            }

            while (_downloader.CheckpointStateHashes is null || _downloader.CheckpointStateHashes.Count < 6)
            {
                Thread.Sleep(1000);
            }
            if (_downloader.CheckpointStateHashes.Count != 6)
            {
                Logger.LogDebug($"Got {_downloader.CheckpointStateHashes.Count} state hash for checkpoint, need only 6.");
                foreach (var (stateHash, checkpointType) in _downloader.CheckpointStateHashes)
                {
                    Logger.LogDebug($"Got state hash {stateHash.ToHex()} for {checkpointType}");
                }
                Logger.LogDebug("Something went wrong while downloading checkpoint state hashes.");
                return false;
            }
            bool match = true;
            foreach (var (expectedStateHash, checkpointType) in stateHashes)
            {
                match &= MatchStateHash(expectedStateHash, checkpointType, _downloader.CheckpointStateHashes);
            }
            if (!match) Logger.LogTrace("Checkpoint state hash mismatch");
            Logger.LogTrace($"Finished verifying checkpoint information, result: {match}");
            return match;
        }
    }
}
