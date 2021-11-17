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
        public static void StartSync(IStateManager stateManager,
                                     IRocksDbContext dbContext,
                                     ISnapshotIndexRepository snapshotIndexRepository,
                                     VersionFactory versionFactory,
                                     ulong blockNumber,
                                     List<string> urls)
        {
            if(Alldone(dbContext))
            {
                Console.WriteLine("Fast Sync was done previously\nReturning");
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
