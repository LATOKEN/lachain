using System;
using System.Collections.Generic;
using Lachain.Logger;
using Lachain.Storage.State;
using Lachain.Storage;
using Lachain.Storage.Trie;
using Lachain.Storage.Repositories;
using Lachain.Utility.Utils;

namespace Lachain.Core.Network.FastSynchronizerBatch
{
    
    public class FastSynchronizerBatch
    {
        private static readonly ILogger<FastSynchronizer> Logger = LoggerFactory.GetLoggerForClass<FastSynchronizer>();
        public static void StartSync(IStateManager stateManager,
                                     IRocksDbContext dbContext,
                                     ISnapshotIndexRepository snapshotIndexRepository,
                                     VersionFactory versionFactory,
                                     ulong blockNumber)
        {
            dbContext.Save(EntryPrefix.NodesDownloadedTillNow.BuildPrefix(), UInt64Utils.ToBytes(0));
            List<string> devnetNodes = new List<string>
            {
                "http://157.245.160.201:7070",
                "http://95.217.6.171:7070",
                "http://88.99.190.191:7070",
                "http://94.130.78.183:7070",
                "http://94.130.24.163:7070",
                "http://94.130.110.127:7070",
                "http://94.130.110.95:7070",
                "http://94.130.58.63:7070",
                "http://88.99.86.166:7070",
                "http://88.198.78.106:7070",
                "http://88.198.78.141:7070",
                "http://88.99.126.144:7070",
                "http://88.99.87.58:7070",
                "http://95.217.6.234:7070"
            };
//            List <string> onlyonenode = new List<string>
            List<string> localnetNodes = new List<string>
            { 
                "http://127.0.0.1:7070",
                "http://127.0.0.1:7071",
                "http://127.0.0.1:7072"
            };

            var snapshot = stateManager.NewSnapshot();
            ISnapshot[] snapshots = new ISnapshot[]{snapshot.Balances,
                                                        snapshot.Contracts,
                                                        snapshot.Storage,
                                                        snapshot.Transactions,
                                                        snapshot.Events,
                                                        snapshot.Validators,
                                                        };

            List<string> urls = localnetNodes;
            HybridQueue hybridQueue = new HybridQueue(dbContext);
            PeerManager peerManager = new PeerManager(urls);
            NodeStorage nodeStorage = new NodeStorage(dbContext, versionFactory);
            RequestManager requestManager = new RequestManager(nodeStorage, hybridQueue);
            Downloader downloader = new Downloader(peerManager, requestManager, blockNumber);

            string[] trieNames = new string[]
            {
                "Balances", "Contracts", "Storage", "Transactions", "Events", "Validators"
            };

            
            downloader.DownloadBlocks(nodeStorage, snapshot.Blocks);

            for(int i = 0; i < trieNames.Length; i++)
            {
                Logger.LogWarning($"Starting trie {trieNames[i]}");
                string rootHash = downloader.GetTrie(trieNames[i], nodeStorage);
                bool foundRoot = nodeStorage.GetIdByHash(rootHash, out ulong curTrieRoot);
                snapshots[i].SetCurrentVersion(curTrieRoot);
                Logger.LogWarning($"Ending trie {trieNames[i]} : {curTrieRoot}");
                Logger.LogWarning($"Max Queue Size {requestManager.maxQueueSize}");
                Logger.LogWarning($"Total Nodes downloaded: {versionFactory.CurrentVersion}");
            }

            blockNumber = Convert.ToUInt64(downloader.GetBlockNumber(), 16);
            stateManager.Approve();
            stateManager.Commit();
            snapshotIndexRepository.SaveSnapshotForBlock(blockNumber, snapshot);
            Logger.LogWarning($"Set state to block {blockNumber} complete");
        }
    }
}
