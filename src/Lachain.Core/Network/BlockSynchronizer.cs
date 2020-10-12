using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Lachain.Logger;
using Lachain.Core.Blockchain.Error;
using Lachain.Core.Blockchain.Interface;
using Lachain.Core.Blockchain.Pool;
using Lachain.Networking;
using Lachain.Proto;
using Lachain.Storage.State;
using Lachain.Utility.Utils;
using NLog;

namespace Lachain.Core.Network
{
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

        private readonly object _peerHasTransactions = new object();
        private readonly object _peerHasBlocks = new object();
        private readonly object _blocksLock = new object();
        private readonly object _txLock = new object();
        private LogLevel _logLevelForSync = LogLevel.Trace;
        private bool _running;
        private readonly Thread _workerThread;

        private readonly IDictionary<ECDSAPublicKey, ulong> _peerHeights
            = new ConcurrentDictionary<ECDSAPublicKey, ulong>();

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
            _workerThread = new Thread(Worker);
        }

        public uint WaitForTransactions(IEnumerable<UInt256> transactionHashes, TimeSpan timeout)
        {
            var txHashes = transactionHashes as UInt256[] ?? transactionHashes.ToArray();
            var lostTxs = _GetMissingTransactions(txHashes);

            const int maxPeersToAsk = 3;
            var maxHeight = _peerHeights.Values.Count == 0 ? 0 : _peerHeights.Values.Max();
            var rnd = new Random();
            var peers = _peerHeights
                .Where(entry => entry.Value >= maxHeight)
                .Select(entry => entry.Key)
                .OrderBy(_ => rnd.Next())
                .Take(maxPeersToAsk)
                .ToArray();

            if (lostTxs.Count == 0) return (uint) txHashes.Length;
            Logger.LogTrace($"Sending query for {lostTxs.Count} transactions to {peers.Length} peers");
            var request = _networkManager.MessageFactory.SyncPoolRequest(lostTxs);
            foreach (var peer in peers) _networkManager.SendTo(peer, request);

            var endWait = DateTime.UtcNow.Add(timeout);
            while (_GetMissingTransactions(txHashes).Count != 0)
            {
                lock (_peerHasTransactions)
                {
                    var timeToWait = endWait.Subtract(DateTime.UtcNow);
                    if (timeToWait.TotalMilliseconds < 0)
                        timeToWait = TimeSpan.Zero;
                    Monitor.Wait(_peerHasTransactions, timeToWait);
                }

                if (DateTime.UtcNow.CompareTo(endWait) > 0) break;
            }

            return (uint) (txHashes.Length - (uint) _GetMissingTransactions(txHashes).Count);
        }

        public uint HandleTransactionsFromPeer(IEnumerable<TransactionReceipt> transactions, ECDSAPublicKey publicKey)
        {
            lock (_txLock)
            {
                var txs = transactions.ToArray();
                Logger.LogTrace($"Received {txs.Length} transactions from peer {publicKey.ToHex()}");
                var persisted = 0u;
                foreach (var tx in txs)
                {
                    if (tx.Signature.IsZero())
                    {
                        Logger.LogTrace($"Received zero-signature transaction: {tx.Hash.ToHex()}");
                        if (_transactionPool.Add(tx) == OperatingError.Ok)
                            persisted++;
                        continue;
                    }

                    var error = _transactionManager.Verify(tx);
                    if (error != OperatingError.Ok)
                    {
                        Logger.LogTrace($"Unable to verify transaction: {tx.Hash.ToHex()} ({error})");
                        continue;
                    }

                    error = _transactionPool.Add(tx);
                    if (error == OperatingError.Ok)
                        persisted++;
                    else
                        Logger.LogTrace($"Transaction {tx.Hash.ToHex()} not persisted: {error}");
                }

                lock (_peerHasTransactions)
                    Monitor.PulseAll(_peerHasTransactions);
                Logger.LogTrace($"Persisted {persisted} transactions from peer {publicKey.ToHex()}");
                return persisted;
            }
        }

        public bool HandleBlockFromPeer(BlockInfo blockWithTransactions, ECDSAPublicKey publicKey)
        {
            lock (_blocksLock)
            {
                var block = blockWithTransactions.Block;
                var receipts = blockWithTransactions.Transactions;
                Logger.LogDebug(
                    $"Got block {block.Header.Index} with hash {block.Hash.ToHex()} from peer {publicKey.ToHex()}");
                var myHeight = _blockManager.GetHeight();
                if (block.Header.Index != myHeight + 1)
                    return false;
                if (!block.TransactionHashes.ToHashSet().SetEquals(receipts.Select(r => r.Hash)))
                    return false;
                var error = _stateManager.SafeContext(() =>
                {
                    if (_blockManager.GetHeight() + 1 == block.Header.Index)
                        return _blockManager.Execute(block, receipts, commit: true, checkStateHash: true);
                    Logger.LogTrace(
                        $"We have blockchain with height {_blockManager.GetHeight()} but got block {block.Header.Index}");
                    return OperatingError.BlockAlreadyExists;
                });
                if (error == OperatingError.BlockAlreadyExists)
                    return true;
                if (error != OperatingError.Ok)
                {
                    Logger.LogWarning(
                        $"Unable to persist block {block.Header.Index} (current height {_blockManager.GetHeight()}), got error {error}, dropping peer");
                    return false;
                }

                lock (_peerHasBlocks)
                    Monitor.PulseAll(_peerHasBlocks);
                return true;
            }
        }

        public void HandlePeerHasBlocks(ulong blockHeight, ECDSAPublicKey publicKey)
        {
            Logger.Log(_logLevelForSync, $"Peer {publicKey.ToHex()} has height {blockHeight}");
            lock (_peerHasBlocks)
            {
                if (_peerHeights.TryGetValue(publicKey, out var peerHeight) && blockHeight <= peerHeight)
                    return;
                _peerHeights[publicKey] = blockHeight;
                Monitor.PulseAll(_peerHasBlocks);
            }
        }

        public bool IsSynchronizingWith(IEnumerable<ECDSAPublicKey> peers)
        {
            var myHeight = _blockManager.GetHeight();
            if (myHeight > _networkManager.LocalNode.BlockHeight)
                _networkManager.LocalNode.BlockHeight = myHeight;
            var setOfPeers = peers.ToHashSet();
            if (setOfPeers.Count == 0) return false;

            var messageFactory = _networkManager.MessageFactory ?? throw new InvalidOperationException();
            _networkBroadcaster.Broadcast(messageFactory.PingRequest(TimeUtils.CurrentTimeMillis(), myHeight));
            lock (_peerHasBlocks)
                Monitor.Wait(_peerHasBlocks, TimeSpan.FromSeconds(1));
            var validatorPeers = _peerHeights
                .Where(entry => setOfPeers.Contains(entry.Key))
                .ToArray();
            Logger.LogDebug($"Got {validatorPeers.Length} connected out of {setOfPeers.Count}");
            if (validatorPeers.Length < setOfPeers.Count * 2 / 3)
                return true;
            if (!validatorPeers.Any()) return false;
            var maxHeight = validatorPeers.Max(v => v.Value);
            Logger.LogDebug($"Max height among peers: {maxHeight}, my height: {myHeight}");
            return myHeight < maxHeight;
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

        private void Worker()
        {
            var rnd = new Random();
            _running = true;
            Logger.LogDebug("Starting block synchronization worker");
            while (_running)
            {
                try
                {
                    var myHeight = _blockManager.GetHeight();
                    if (myHeight > _networkManager.LocalNode.BlockHeight)
                        _networkManager.LocalNode.BlockHeight = myHeight;

                    var messageFactory = _networkManager.MessageFactory ?? throw new InvalidOperationException();
                    _networkBroadcaster.Broadcast(messageFactory.PingRequest(TimeUtils.CurrentTimeMillis(), myHeight));
                    lock (_peerHasBlocks)
                        Monitor.Wait(_peerHasBlocks, TimeSpan.FromSeconds(1));
                    if (_peerHeights.Count == 0)
                    {
                        Logger.LogWarning("Peer height map is empty, nobody responds to pings?");
                        continue;
                    }

                    var maxHeight = _peerHeights.Values.Max();
                    if (myHeight >= maxHeight)
                    {
                        Logger.LogTrace($"Nothing to do: my height is {myHeight} and peers are at {maxHeight}");
                        continue;
                    }

                    const int maxPeersToAsk = 3;
                    const int maxBlocksToRequest = 5;

                    var peers = _peerHeights
                        .Where(entry => entry.Value >= maxHeight)
                        .Select(entry => entry.Key)
                        .OrderBy(_ => rnd.Next())
                        .Take(maxPeersToAsk)
                        .ToArray();

                    Logger.LogTrace($"Sending query for blocks to {peers.Length} peers");
                    foreach (var peer in peers)
                    {
                        var leftBound = myHeight + 1;
                        var rightBound = Math.Min(maxHeight, myHeight + maxBlocksToRequest);
                        _networkManager.SendTo(peer, messageFactory.SyncBlocksRequest(leftBound, rightBound));
                    }

                    lock (_peerHasBlocks)
                        Monitor.Wait(_peerHasBlocks, TimeSpan.FromSeconds(1));
                }
                catch (Exception e)
                {
                    Logger.LogError($"Error in block synchronizer: {e}");
                }

                Thread.Sleep(5_000);
            }
        }

        public void Start()
        {
            _workerThread.Start();
        }

        private List<UInt256> _GetMissingTransactions(IEnumerable<UInt256> txHashes)
        {
            return txHashes
                .Where(hash => (_transactionManager.GetByHash(hash) ?? _transactionPool.GetByHash(hash)) is null)
                .ToList();
        }

        public void Dispose()
        {
            _running = false;
            if (_workerThread.ThreadState == ThreadState.Running)
                _workerThread.Join();
        }
    }
}