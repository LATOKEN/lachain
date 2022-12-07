using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Google.Protobuf;
using Lachain.Logger;
using Lachain.Core.Blockchain.Error;
using Lachain.Core.Blockchain.Hardfork;
using Lachain.Core.Blockchain.Interface;
using Lachain.Core.Blockchain.Pool;
using Lachain.Core.Consensus;
using Lachain.Networking;
using Lachain.Proto;
using Lachain.Storage.State;
using Lachain.Utility;
using Lachain.Utility.Utils;
using NLog;

namespace Lachain.Core.Network
{
    /*
        BlockSynchroniser makes sure that the node is always up to date with the rest of the network.
        It continuesly asks to the peers for their height and if their height is greater than its
        height, then it requests for the new blocks. Upon receiving them, it tries to execute the 
        block and adds to the chain.
    */
    public class BlockSynchronizer : IBlockSynchronizer
    {
        private static readonly ILogger<BlockSynchronizer>
            Logger = LoggerFactory.GetLoggerForClass<BlockSynchronizer>();

        private readonly ITransactionManager _transactionManager;
        private readonly IBlockManager _blockManager;
        private readonly INetworkBroadcaster _networkBroadcaster;
        private readonly INetworkManager _networkManager;
        private readonly ITransactionPool _transactionPool;
        private readonly IStateManager _stateManager;
        private readonly object _peerHasBlocks = new object();
        private readonly object _peerHeightUpdate = new object();
        private LogLevel _logLevelForSync = LogLevel.Trace;
        private bool _running;
        private readonly Thread _blockSyncThread;
        private readonly Thread _pingThread;
        private readonly Thread _blockFromPeerThread;
        private readonly Thread _txFromPeerThread;

        private readonly IDictionary<ECDSAPublicKey, ulong> _peerHeights
            = new ConcurrentDictionary<ECDSAPublicKey, ulong>();
        private readonly Queue<(SyncBlocksReply, ECDSAPublicKey)> _blockFromPeer
            = new Queue<(SyncBlocksReply, ECDSAPublicKey)>();

        private readonly Queue<(SyncPoolReply, ECDSAPublicKey)> _txFromPeer
            = new Queue<(SyncPoolReply, ECDSAPublicKey)>();

        public BlockSynchronizer(
            ITransactionManager transactionManager,
            IBlockManager blockManager,
            INetworkBroadcaster networkBroadcaster,
            INetworkManager networkManager,
            ITransactionPool transactionPool,
            IStateManager stateManager
        )
        {
            _transactionManager = transactionManager;
            _blockManager = blockManager;
            _networkBroadcaster = networkBroadcaster;
            _networkManager = networkManager;
            _transactionPool = transactionPool;
            _stateManager = stateManager;
            _blockSyncThread = new Thread(BlockSyncWorker);
            _pingThread = new Thread(PingWorker);
            _blockFromPeerThread = new Thread(BlockFromPeerWorker);
            _txFromPeerThread = new Thread(TransactionsFromPeerWorker);
        }

        public event EventHandler<ulong>? OnSignedBlockReceived;

        public void TxReceivedFromPeer(SyncPoolReply reply, ECDSAPublicKey peer)
        {
            lock (_txFromPeer)
            {
                _txFromPeer.Enqueue((reply, peer));
                Monitor.PulseAll(_txFromPeer);
            }
        }

        private void TransactionsFromPeerWorker()
        {
            Logger.LogDebug("Starting TransactionsFromPeerWorker");
            while (_running)
            {
                ECDSAPublicKey? peer = null;
                try
                {
                    SyncPoolReply reply;
                    lock (_txFromPeer)
                    {
                        while (_txFromPeer.Count == 0)
                        {
                            Monitor.Wait(_txFromPeer);
                            if (!_running)
                                break;
                        }
                        (reply, peer) = _txFromPeer.Dequeue();
                        var txs = (reply.Transactions ?? Enumerable.Empty<TransactionReceipt>())
                            .OrderBy(tx => new ReceiptComparer()).ToArray();

                        Logger.LogTrace($"Received {txs.Length} transactions from peer {peer.ToHex()}");
                        var persisted = 0;
                        foreach (var tx in txs)
                        {
                            if (!HandleTransactionsFromPeer(tx, peer))
                                break;
                            persisted++;
                        }

                        Logger.LogTrace($"Persisted {persisted} transactions from peer {peer.ToHex()}");
                    }
                }
                catch (Exception exc)
                {
                    Logger.LogWarning(
                        $"Got exception trying to process transactions from peer {(peer is null ? "null" : peer.ToHex())}: {exc}"
                    );
                }
            }
        }

        private bool HandleTransactionsFromPeer(TransactionReceipt transaction, ECDSAPublicKey publicKey)
        {
            var error = _transactionPool.Add(transaction, false);
            if (error == OperatingError.Ok)
                return true;
            else
            {
                Logger.LogTrace($"Transaction {transaction.Hash.ToHex()} not persisted from peer {publicKey.ToHex()}: {error}");
                return false;
            }
        }

        public void BlockReceivedFromPeer(SyncBlocksReply reply, ECDSAPublicKey peer)
        {
            lock (_blockFromPeer)
            {
                _blockFromPeer.Enqueue((reply, peer));
                Monitor.PulseAll(_blockFromPeer);
            }
        }

        private void BlockFromPeerWorker()
        {
            Logger.LogDebug("Starting BlockFromPeerWorker");
            while (_running)
            {
                ECDSAPublicKey? peer = null;
                try
                {
                    SyncBlocksReply reply;
                    lock (_blockFromPeer)
                    {
                        while (_blockFromPeer.Count == 0)
                        {
                            Monitor.Wait(_blockFromPeer);
                            if (!_running)
                                break;
                        }
                        (reply, peer) = _blockFromPeer.Dequeue();
                    }

                    var len = reply.Blocks?.Count ?? 0;
                    var orderedBlocks = (reply.Blocks ?? Enumerable.Empty<BlockInfo>())
                        .Where(x => x.Block?.Header?.Index != null)
                        .OrderBy(x => x.Block.Header.Index)
                        .ToArray();
                    Logger.LogTrace($"Blocks received: {orderedBlocks.Length} ({len})");
                    foreach (var block in orderedBlocks)
                    {
                        if (!HandleBlockFromPeer(block, peer))
                            break;
                    }
                }
                catch (Exception exc)
                {
                    Logger.LogWarning(
                        $"Got exception trying to process blocks from peer {(peer is null ? "null" : peer.ToHex())}: {exc}"
                    );
                }
            }
        }

        private bool HandleBlockFromPeer(BlockInfo blockWithTransactions, ECDSAPublicKey publicKey)
        {
            Logger.LogTrace("HandleBlockFromPeer");
            var block = blockWithTransactions.Block;
            var receipts = blockWithTransactions.Transactions;
            Logger.LogDebug(
                $"Got block {block.Header.Index} with hash {block.Hash.ToHex()} from peer {publicKey.ToHex()}");
            var myHeight = _blockManager.GetHeight();
            if (block.Header.Index != myHeight + 1)
            {
                Logger.LogTrace(
                    $"Skipped block {block.Header.Index} from peer {publicKey.ToHex()}: our height is {myHeight}");
                return false;
            }

            if (!block.TransactionHashes.ToHashSet().SetEquals(receipts.Select(r => r.Hash)))
            {
                try
                {
                    var needHashes = string.Join(", ", block.TransactionHashes.Select(x => x.ToHex()));
                    var gotHashes = string.Join(", ", receipts.Select(x => x.Hash.ToHex()));

                    Logger.LogTrace(
                        $"Skipped block {block.Header.Index} from peer {publicKey.ToHex()}: expected hashes [{needHashes}] got hashes [{gotHashes}]");
                }
                catch (Exception e)
                {
                    Logger.LogWarning($"Failed to get transaction receipts for tx hash: {e}");
                }

                return false;
            }

            var error = _blockManager.VerifySignatures(block, true);
            if (error != OperatingError.Ok)
            {
                Logger.LogTrace($"Skipped block {block.Header.Index} from peer {publicKey.ToHex()}: invalid multisig with error: {error}");
                return false;
            }
            // let it know that we received a valid response
            lock (_peerHasBlocks)
                Monitor.PulseAll(_peerHasBlocks);
            // This is to tell consensus manager to terminate current era, since we trust given multisig
            OnSignedBlockReceived?.Invoke(this, block.Header.Index);

            error = _stateManager.SafeContext(() =>
            {
                if (_blockManager.GetHeight() + 1 == block.Header.Index)
                    return _blockManager.Execute(block, receipts, commit: true, checkStateHash: true);
                Logger.LogTrace(
                    $"We have blockchain with height {_blockManager.GetHeight()} but got block {block.Header.Index}");
                return OperatingError.BlockAlreadyExists;
            });
            if (error == OperatingError.BlockAlreadyExists)
            {
                Logger.LogTrace(
                    $"Skipped block {block.Header.Index} from peer {publicKey.ToHex()}: block already exists");
                return false;
            }

            if (error != OperatingError.Ok)
            {
                Logger.LogWarning(
                    $"Unable to persist block {block.Header.Index} (current height {_blockManager.GetHeight()}), got error {error}, dropping peer");
                return false;
            }

            return true;
        }

        public void HandlePeerHasBlocks(ulong blockHeight, ECDSAPublicKey publicKey)
        {
            Logger.Log(_logLevelForSync, $"Peer {publicKey.ToHex()} has height {blockHeight}");
            lock (_peerHeightUpdate)
            {
                if (_peerHeights.TryGetValue(publicKey, out var peerHeight) && blockHeight <= peerHeight)
                    return;
                _peerHeights[publicKey] = blockHeight;
                Monitor.PulseAll(_peerHeightUpdate);
            }
        }

        public bool IsSynchronizingWith(IEnumerable<ECDSAPublicKey> peers)
        {
            var myHeight = _blockManager.GetHeight();
            if (myHeight > _networkManager.LocalNode.BlockHeight)
                _networkManager.LocalNode.BlockHeight = myHeight;
            var setOfPeers = peers.ToHashSet();
            if (setOfPeers.Count == 0) return false;

            lock (_peerHeightUpdate)
                Monitor.Wait(_peerHeightUpdate, TimeSpan.FromSeconds(1));
            var validatorPeers = _peerHeights
                .Where(entry => setOfPeers.Contains(entry.Key))
                .ToArray();
            Logger.LogDebug($"Got {validatorPeers.Length} connected out of {setOfPeers.Count}");
            if (validatorPeers.Length < setOfPeers.Count * 2 / 3)
                return true;
            if (!validatorPeers.Any()) return false;

            List<ulong> heights = new List<ulong>();
            foreach (var keyValuePair in validatorPeers)
            {
                heights.Add(keyValuePair.Value);
            }
            heights.Sort();
            var medianHeight = heights[heights.Count / 2];
            Logger.LogDebug($"Median height among peers: {medianHeight}, my height: {myHeight}");
            return myHeight < medianHeight;
        }

        public void SynchronizeWith(IEnumerable<ECDSAPublicKey> peers)
        {
            var peersArray = peers.ToArray();
            _logLevelForSync = LogLevel.Debug;
            Logger.LogDebug($"Synchronizing with peers: {string.Join(", ", peersArray.Select(x => x.ToHex()))}");
            while (IsSynchronizingWith(peersArray))
            {
                Thread.Sleep(TimeSpan.FromMilliseconds(1_000));
            }

            _logLevelForSync = LogLevel.Trace;
        }

        public ulong? GetHighestBlock()
        {
            if (_peerHeights.Count == 0) return null;
            return _peerHeights.Max(v => v.Value);
        }

        public IDictionary<ECDSAPublicKey, ulong> GetConnectedPeers()
        {
            return _peerHeights;
        }

        private void PingWorker()
        {
            while (_running)
            {
                try
                {
                    var reply = _networkManager.MessageFactory.PingReply(
                        TimeUtils.CurrentTimeMillis(), _stateManager.LastApprovedSnapshot.Blocks.GetTotalBlockHeight()
                    );
                    _networkBroadcaster.Broadcast(reply, NetworkMessagePriority.PeerSyncMessage);
                    Logger.LogTrace($"Broadcasted our height: {reply.PingReply.BlockHeight}");
                }
                catch (Exception e)
                {
                    Logger.LogError($"Error in ping worker: {e}");
                }

                Thread.Sleep(TimeSpan.FromMilliseconds(3_000));
            }
        }

        private void BlockSyncWorker()
        {
            var rnd = new Random();
            Logger.LogDebug("Starting block synchronization worker");
            while (_running)
            {
                try
                {
                    var myHeight = _blockManager.GetHeight();
                    if (myHeight > _networkManager.LocalNode.BlockHeight)
                        _networkManager.LocalNode.BlockHeight = myHeight;

                    if (_peerHeights.Count == 0)
                    {
                        Logger.LogWarning("Peer height map is empty, nobody responds to pings?");
                        Thread.Sleep(TimeSpan.FromMilliseconds(1_000));
                        continue;
                    }

                    var maxHeight = _peerHeights.Values.Max();
                    if (myHeight >= maxHeight)
                    {
                        Logger.LogTrace($"Nothing to do: my height is {myHeight} and peers are at {maxHeight}");
                        Thread.Sleep(TimeSpan.FromMilliseconds(1_000));
                        continue;
                    }

                    const int maxPeersToAsk = 1;
                    const int maxBlocksToRequest = 10;

                    var peers = _peerHeights
                        .Where(entry => entry.Value > myHeight)
                        .OrderBy(_ => rnd.Next())
                        .Take(maxPeersToAsk)
                        .ToArray();

                    var leftBound = myHeight + 1;
                    foreach (var peer in peers)
                    {
                        var rightBound = Math.Min(peer.Value, myHeight + maxBlocksToRequest);
                        Logger.LogTrace($"Sending query for blocks [{leftBound}; {rightBound}] to peer {peer.Key.ToHex()}");
                        _networkManager.SendTo(
                            peer.Key, _networkManager.MessageFactory.SyncBlocksRequest(leftBound, rightBound),
                            NetworkMessagePriority.PeerSyncMessage
                        );
                    }

                    var waitForBlockExecution = 4000;
                    bool gotResponse = true;
                    lock (_peerHasBlocks)
                    {
                        gotResponse = Monitor.Wait(_peerHasBlocks, TimeSpan.FromMilliseconds(1_000));
                    }
                    if (gotResponse)
                    {
                        Thread.Sleep(waitForBlockExecution);
                    }
                }
                catch (Exception e)
                {
                    Logger.LogError($"Error in block synchronizer: {e}");
                    Thread.Sleep(1_000);
                }
            }
        }

        public void Start()
        {
            _running = true;
            _blockSyncThread.Start();
            _pingThread.Start();
            _blockFromPeerThread.Start();
            _txFromPeerThread.Start();
        }

        private void TerminateBlockFromPeerWorker()
        {
            // in case BlockFromPeerWorker is stuck for waiting new msg
            lock (_blockFromPeer)
                Monitor.PulseAll(_blockFromPeer);
            if (_blockFromPeerThread.ThreadState == ThreadState.Running)
                _blockFromPeerThread.Join();
        }

        private void TerminateTxFromPeerWorker()
        {
            // in case TxFromPeerWorker is stuck for waiting new msg
            lock (_txFromPeer)
                Monitor.PulseAll(_txFromPeer);
            if (_txFromPeerThread.ThreadState == ThreadState.Running)
                _txFromPeerThread.Join();
        }
        
        public void Dispose()
        {
            _running = false;
            if (_blockSyncThread.ThreadState == ThreadState.Running)
                _blockSyncThread.Join();
            if (_pingThread.ThreadState == ThreadState.Running)
                _pingThread.Join();
            TerminateBlockFromPeerWorker();
            TerminateTxFromPeerWorker();
        }
    }
}