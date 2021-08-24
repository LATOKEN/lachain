using System.Collections.Generic;
using Lachain.Logger;
using Lachain.Storage.State;
using Lachain.Storage.Trie;
using Lachain.Storage.Repositories;

namespace Lachain.Core.Network
{
    public class FastSynchronizer
    {
        private static readonly ILogger<FastSynchronizer> Logger = LoggerFactory.GetLoggerForClass<FastSynchronizer>();

        public static void FastSync(IStateManager stateManager, ISnapshotIndexRepository snapshotIndexRepository, string peerURL)
        {
            StateDownloader stateDownloader = new StateDownloader(peerURL);
            var blockNumber = stateDownloader.DownloadBlockNumber();
            Logger.LogWarning($"Performing set state to block {blockNumber}");
            var snapshot = stateManager.NewSnapshot();

            string[] trieNames = new string[] { "Balances", "Contracts", "Storage", "Transactions", "Blocks", "Events", "Validators" };
            ISnapshot[] snapshots = new ISnapshot[]{snapshot.Balances,
                                                        snapshot.Contracts,
                                                        snapshot.Storage,
                                                        snapshot.Transactions,
                                                        snapshot.Blocks,
                                                        snapshot.Events,
                                                        snapshot.Validators};

            for (int i = 0; i < trieNames.Length; i++)
            {
                string trieName = trieNames[i];
                ulong curTrieRoot = stateDownloader.DownloadRoot(trieName);
                IDictionary<ulong, IHashTrieNode> curTrie = stateDownloader.DownloadTrie(trieName);
                snapshots[i].SetState(curTrieRoot, curTrie);
                Logger.LogInformation($"{trieName} update done");
            }

            stateManager.Approve();
            stateManager.Commit();
            snapshotIndexRepository.SaveSnapshotForBlock(blockNumber, snapshot);
            Logger.LogWarning($"Set state to block {blockNumber} complete");
        }
    }
}
