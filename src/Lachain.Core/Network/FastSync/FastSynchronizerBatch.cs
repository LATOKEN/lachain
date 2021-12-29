/*
    This file controls the fast_sync, other necessary classes are instantiated here.
    We will be downloading 6 tree data structures, one at a time. Block headers will be downloaded differently, not as a tree.    

    **SCOPE TO IMPROVE**:   1) Blocks are downloaded one by one. This creates lots of unnecessary nodes which can be deleted after
                            set state is complete.
                            2) Tries are downloaded one by one and some trie (Event, Transaction) has many nodes where other tries
                            have few nodes. So the asynchronous process may not fully utilize all available threads. We can try to
                            download tries parallelly (maybe with weight, like Event will go first) to speed up

    **FEATURE TO ADD**:     Fastsync is possible only for the first time. If fastsync is done before, fastsync is not allowed.
                            It can be studied if it is necessary and add features to allow fastsync anytime.
*/
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Lachain.Core.Blockchain.Checkpoints;
using Lachain.Crypto;
using Lachain.Logger;
using Lachain.Storage.State;
using Lachain.Storage;
using Lachain.Utility.Utils;
using Lachain.Utility.Serialization;
using System.Linq;

namespace Lachain.Core.Network.FastSync
{
    
    public class FastSynchronizerBatch
    {
        // the order of trieNames is important to create StateHash
        private readonly string[] trieNames = new string[]
        {
                "Balances", "Contracts", "Storage", "Transactions", "Events", "Validators"
        };
        private static readonly ILogger<FastSynchronizerBatch> Logger = LoggerFactory.GetLoggerForClass<FastSynchronizerBatch>();
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
        [MethodImpl(MethodImplOptions.Synchronized)]
        public void StartSync(ulong? blockNumber, UInt256? blockHash, List<(UInt256, CheckpointType)>? stateHashes)
        {
            if(Alldone(dbContext))
            {
                Logger.LogTrace("Fast Sync was done previously...Returning");
                return;
            }
            dbContext.Save(EntryPrefix.NodesDownloadedTillNow.BuildPrefix(), UInt64Utils.ToBytes(0));
            
            Console.WriteLine("Current Version: "+versionFactory.CurrentVersion);

            NodeStorage nodeStorage = new NodeStorage(dbContext, versionFactory);
            HybridQueue hybridQueue = new HybridQueue(dbContext, nodeStorage);
            PeerManager peerManager = new PeerManager(urls);
            
            RequestManager requestManager = new RequestManager(nodeStorage, hybridQueue);

            ulong savedBlockNumber = GetBlockNumberFromDB(dbContext);
            if(savedBlockNumber!=0) blockNumber = savedBlockNumber;
            Downloader downloader = new Downloader(peerManager, requestManager, blockNumber);

            blockNumber = Convert.ToUInt64(downloader.GetBlockNumber(), 16);
            int downloadedTries = Initialize(dbContext, blockNumber, (savedBlockNumber!=0));
            hybridQueue.init();

            for(int i = downloadedTries; i < trieNames.Length; i++)
            {
                Logger.LogWarning($"Starting trie {trieNames[i]}");
                string rootHash = downloader.GetTrie(trieNames[i], nodeStorage);
                bool foundRoot = nodeStorage.GetIdByHash(rootHash, out ulong curTrieRoot);
            //    snapshots[i].SetCurrentVersion(curTrieRoot);
                downloadedTries++;
                dbContext.Save(EntryPrefix.LastDownloaded.BuildPrefix(), downloadedTries.ToBytes().ToArray());
                Logger.LogWarning($"Ending trie {trieNames[i]} : {curTrieRoot}");
            //    bool isConsistent = requestManager.CheckConsistency(curTrieRoot);
            //    Console.WriteLine("Is Consistent : "+isConsistent );
                Logger.LogWarning($"Total Nodes downloaded: {versionFactory.CurrentVersion}");
            }
            
            if(downloadedTries==(int)trieNames.Length)
            {
                var snapshot = stateManager.NewSnapshot();
                ISnapshot[] snapshots = new ISnapshot[]{snapshot.Balances,
                                                        snapshot.Contracts,
                                                        snapshot.Storage,
                                                        snapshot.Transactions,
                                                        snapshot.Events,
                                                        snapshot.Validators,
                                                        };

                downloader.DownloadBlocks(nodeStorage, snapshot.Blocks);

                for(int i=0; i<trieNames.Length; i++)
                {
                    bool foundHash = nodeStorage.GetIdByHash(downloader.DownloadRootHashByTrieName(trieNames[i]), out ulong trieRoot);
                    snapshots[i].SetCurrentVersion(trieRoot);
                }

                stateManager.Approve();
                stateManager.Commit();
                snapshotIndexRepository.SaveSnapshotForBlock(blockNumber, snapshot);
                
                downloadedTries++;
                SetDownloaded(dbContext, downloadedTries);
                
                Logger.LogWarning($"Set state to block {blockNumber} complete");
            }
        }

        static int Initialize(IRocksDbContext dbContext, ulong blockNumber, bool previousData)
        {
            if(!previousData)
            {
                RocksDbAtomicWrite tx = new RocksDbAtomicWrite(dbContext);
                tx.Put(EntryPrefix.BlockNumber.BuildPrefix(), blockNumber.ToBytes().ToArray());
                ulong zero = 0;
                tx.Put(EntryPrefix.SavedBatch.BuildPrefix(), zero.ToBytes().ToArray());
                tx.Put(EntryPrefix.TotalBatch.BuildPrefix(), zero.ToBytes().ToArray());
                tx.Put(EntryPrefix.LastDownloaded.BuildPrefix(), zero.ToBytes().ToArray());
                tx.Commit();
                return 0;
            }
            var rawId = dbContext.Get(EntryPrefix.LastDownloaded.BuildPrefix());
            return SerializationUtils.ToInt32(rawId);
        }

        static ulong GetBlockNumberFromDB(IRocksDbContext dbContext)
        {
            var rawBlockNumber = dbContext.Get(EntryPrefix.BlockNumber.BuildPrefix());
            if(rawBlockNumber==null) return 0;
            return SerializationUtils.ToUInt64(rawBlockNumber);
        }
        
        static void SetDownloaded(IRocksDbContext dbContext, int downloaded)
        {
            return _repository.GetCheckpointBlockNumber() > 0  && !Alldone();
        }

        static bool Alldone(IRocksDbContext dbContext)
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

        [MethodImpl(MethodImplOptions.Synchronized)]
        public bool IsCheckpointOk(ulong? blockHeight, UInt256? blockHash, List<(UInt256, CheckpointType)>? stateHashes)
        {
            _downloader.ResetCheckpointInfo();
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

            while (_downloader.CheckpointBlock is null)
            {
                Thread.Sleep(1000);
            }
            if (!_downloader.CheckpointBlock.Hash.Equals(blockHash))
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

            var calculatedStateHash = CalculateStateHash(stateHashes);
            if (!calculatedStateHash.Equals(_downloader.CheckpointBlock.Header.StateHash))
            {
                Logger.LogWarning($"StateHash mis match. StateHash from CheckpointBlock: "
                    + $"{_downloader.CheckpointBlock.Header.StateHash.ToHex()}, StateHash from CheckpointStateHashes:"
                    + $" {calculatedStateHash.ToHex()}");
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

        private UInt256 CalculateStateHash(List<(UInt256, CheckpointType)>? stateHashes)
        {
            if (stateHashes is null || stateHashes.Count != 6)
            {
                Logger.LogDebug($"Invalid list of state hashes. List is {(stateHashes is null ? "null" : "not null")}");
                if (!(stateHashes is null))
                {
                    Logger.LogDebug($"List size: {stateHashes.Count}");
                }
                throw new Exception("Invalid list of state hashes");
            }
            var stateHashesList = new List<UInt256>();
            foreach (var trieName in trieNames)
            {
                var hash = GetRootHashForTrieName(trieName, stateHashes);
                if (hash is null)
                {
                    Logger.LogDebug("Invalid list of state hashes: null hash for " + trieName);
                    throw new Exception("Invalid list of state hashes");
                }
                stateHashesList.Add(hash);
            }
            return stateHashesList.Select(hash => hash.ToBytes()).Flatten().Keccak();
        }
    }
}
