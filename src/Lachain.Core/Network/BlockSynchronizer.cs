using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
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
            _networkBroadcaster.Broadcast(_networkManager.MessageFactory?.GetTransactionsByHashesRequest(lostTxs) ??
                                          throw new InvalidOperationException());
            var endWait = DateTime.UtcNow.Add(timeout);
            while (_GetMissingTransactions(txHashes).Count != 0)
            {
                lock (_peerHasTransactions)
                {
                    var timeToWait = endWait.Subtract(DateTime.Now);
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
            var persisted = 0u;
            foreach (var tx in transactions)
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

                if (_transactionPool.Add(tx) == OperatingError.Ok)
                    persisted++;
            }

            lock (_peerHasTransactions)
                Monitor.PulseAll(_peerHasTransactions);
            return persisted;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void HandleBlockFromPeer(Block block, ECDSAPublicKey publicKey)
        {
            Logger.LogDebug($"Got block {block.Header.Index} with hash {block.Hash.ToHex()} from peer {publicKey.ToHex()}");
            var myHeight = _blockManager.GetHeight();
            if (block.Header.Index != myHeight + 1)
                return;
            /* if we don't have transactions from block than request it */
            var haveNotTxs = _GetMissingTransactions(block);
            if (haveNotTxs.Count > 0)
            {
                Logger.LogTrace($"Waiting for {haveNotTxs.Count} transactions not present in block");
                var totalFound = WaitForTransactions(block.TransactionHashes, TimeSpan.FromSeconds(5));
                Logger.LogTrace($"Got {totalFound} transactions out of {haveNotTxs.Count} missing");
                /* if peer can't provide all hashes from block than he might be a liar */
                if (totalFound != haveNotTxs.Count)
                    return;
            }

            /* persist block to database */
            var txs = block.TransactionHashes
                .Select(txHash => _transactionPool.GetByHash(txHash))
                .Where(tx => !(tx is null))
                .Select(tx => tx!)
                .ToList();

            var error = _stateManager.SafeContext(() =>
            {
                if (_blockManager.GetHeight() + 1 != block.Header.Index)
                {
                    Logger.LogTrace(
                        $"We have Blockchain with height {_blockManager.GetHeight()} but got block {block.Header.Index}");
                    return OperatingError.BlockAlreadyExists;
                }

                return _blockManager.Execute(block, txs, commit: true, checkStateHash: true);
            });
            if (error == OperatingError.BlockAlreadyExists)
                return;
            if (error != OperatingError.Ok)
            {
                Logger.LogWarning(
                    $"Unable to persist block {block.Header.Index} (current height {_blockManager.GetHeight()}), got error {error}, dropping peer");
                return;
            }

            lock (_peerHasBlocks)
                Monitor.PulseAll(_peerHasBlocks);
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
            if (validatorPeers.Length < setOfPeers.Count * 2 / 3)
                return true;
            var maxHeight = validatorPeers.Max(v => v.Value);
            return myHeight < maxHeight;
        }

        public void SynchronizeWith(IEnumerable<ECDSAPublicKey> peers)
        {
            var peersArray = peers.ToArray();
            _logLevelForSync = LogLevel.Debug;
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

        private void Worker()
        {
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
                        return;

                    var maxHeight = _peerHeights.Values.Max(v => v);
                    if (myHeight >= maxHeight)
                        return;

                    const int maxPeersToAsk = 3;
                    const int maxBlocksToRequest = 100;
            
                    var rnd = new Random();
                    var peers = _peerHeights
                        .Where(entry => entry.Value == maxHeight)
                        .Select(entry => entry.Key)
                        .OrderBy(_ => rnd.Next())
                        .Take(maxPeersToAsk);

                    foreach (var peer in peers)
                    {
                        var leftBound = myHeight + 1;
                        var rightBound = Math.Min(maxHeight, myHeight + maxBlocksToRequest);
                        _networkManager.SendTo(peer, messageFactory.GetBlocksByHeightRangeRequest(leftBound, rightBound));
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

        private List<UInt256> _GetMissingTransactions(Block block)
        {
            return _GetMissingTransactions(block.TransactionHashes);
        }

        public void Dispose()
        {
            _running = false;
            _workerThread.Join();
        }
    }
}