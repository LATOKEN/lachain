/*
    This file controls the fast_sync, other necessary classes are instantiated here.
    We will be downloading 6 tree data structures, one at a time. Block headers will be downloaded differently, not as a tree.    
*/
using System;
using System.Collections.Generic;
using Lachain.Logger;
using Lachain.Storage.State;
using Lachain.Storage;
using Lachain.Storage.Trie;
using Lachain.Storage.Repositories;
using Lachain.Utility.Utils;
using Lachain.Utility.Serialization;
using System.Linq;

namespace Lachain.Core.Network.FastSynchronizerBatch
{
    
    public class FastSynchronizerBatch
    {
        static string[] trieNames = new string[]
        {
                "Balances", "Contracts", "Storage", "Transactions", "Events", "Validators"
        };
        private static readonly ILogger<FastSynchronizer> Logger = LoggerFactory.GetLoggerForClass<FastSynchronizer>();

        //Fast_sync is started from this function.
        //urls is the list of peer nodes, we'll be requesting for data throughtout this process
        //blockNumber denotes which block we want to sync with, if it is 0, we will ask for the latest block number to a random peer and
        //start synching with that peer
        public static void StartSync(IStateManager stateManager,
                                     IRocksDbContext dbContext,
                                     ISnapshotIndexRepository snapshotIndexRepository,
                                     VersionFactory versionFactory,
                                     ulong blockNumber,
                                     List<string> urls)
        {
            //At first we check if fast sync have started and completed before.
            // If it has completed previously, we don't let the user run it again.
            if(Alldone(dbContext))
            {
                Console.WriteLine("Fast Sync was done previously\nReturning");
                return;
            }
            
            Console.WriteLine("Current Version: "+versionFactory.CurrentVersion);

            NodeStorage nodeStorage = new NodeStorage(dbContext, versionFactory);
            HybridQueue hybridQueue = new HybridQueue(dbContext, nodeStorage);
            PeerManager peerManager = new PeerManager(urls);
            
            RequestManager requestManager = new RequestManager(nodeStorage, hybridQueue);

            //If fast_sync was started previously, then this variable should contain which block number we are trying to sync with, otherwise 0.
            //If it is non-zero, then we will forcefully sync with that block irrespective of what the user input for blockNumber is now.
            ulong savedBlockNumber = GetBlockNumberFromDB(dbContext);
            if(savedBlockNumber!=0) blockNumber = savedBlockNumber;
            Downloader downloader = new Downloader(peerManager, requestManager, blockNumber);
            //this line is only useful if fast_sync was not started previously and user wants to sync with latest block
            blockNumber = downloader.GetBlockNumber();
            
            //to keep track how many tries have been downloaded till now, saved in db with LastDownloaded prefix
            int downloadedTries = Initialize(dbContext, blockNumber, (savedBlockNumber!=0));
            hybridQueue.init();

            for(int i = downloadedTries; i < trieNames.Length; i++)
            {
                Logger.LogWarning($"Starting trie {trieNames[i]}");
                var rootHash = downloader.GetTrie(trieNames[i], nodeStorage);
                bool foundRoot = nodeStorage.GetIdByHash(rootHash.ToHex(), out ulong curTrieRoot);
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
                    bool foundHash = nodeStorage.GetIdByHash(downloader.DownloadRootHashByTrieName(trieNames[i]).ToHex(), out ulong trieRoot);
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
            dbContext.Save(EntryPrefix.LastDownloaded.BuildPrefix(), downloaded.ToBytes().ToArray());
        }

        static bool Alldone(IRocksDbContext dbContext)
        {
            var rawInfo = dbContext.Get(EntryPrefix.LastDownloaded.BuildPrefix());
            if(rawInfo == null) return false;
            return SerializationUtils.ToInt32(rawInfo) == ((int)trieNames.Length + 1) ;
        }
    }
}
