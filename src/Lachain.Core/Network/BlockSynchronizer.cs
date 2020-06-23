using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
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

namespace Lachain.Core.Network
{
    public class BlockSynchronizer : IBlockSynchronizer
    {
        private static readonly ILogger<BlockSynchronizer>
            Logger = LoggerFactory.GetLoggerForClass<BlockSynchronizer>();

        private readonly ITransactionManager _transactionManager;
        private readonly IBlockManager _blockManager;
        private readonly INetworkContext _networkContext;
        private readonly INetworkBroadcaster _networkBroadcaster;
        private readonly INetworkManager _networkManager;
        private readonly ITransactionPool _transactionPool;
        private readonly IStateManager _stateManager;

        private readonly IDictionary<IRemotePeer, ulong> _peerHeights
            = new ConcurrentDictionary<IRemotePeer, ulong>();

        private readonly object _peerHasTransactions = new object();
        private readonly object _peerHasBlocks = new object();

        public BlockSynchronizer(
            ITransactionManager transactionManager,
            IBlockManager blockManager,
            INetworkContext networkContext,
            INetworkBroadcaster networkBroadcaster,
            INetworkManager networkManager,
            ITransactionPool transactionPool,
            IStateManager stateManager)
        {
            _transactionManager = transactionManager;
            _blockManager = blockManager;
            _networkContext = networkContext;
            _networkBroadcaster = networkBroadcaster;
            _networkManager = networkManager;
            _transactionPool = transactionPool;
            _stateManager = stateManager;
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

        public uint HandleTransactionsFromPeer(IEnumerable<TransactionReceipt> transactions, IRemotePeer remotePeer)
        {
            var persisted = 0u;
            foreach (var tx in transactions)
            {
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

        public uint WaitForBlocks(IEnumerable<UInt256> blockHashes, TimeSpan timeout)
        {
            throw new NotImplementedException();
        }

        public void HandleBlockFromPeer(Block block, IRemotePeer remotePeer, TimeSpan timeout)
        {
            var myHeight = _blockManager.GetHeight();
            if (block.Header.Index != myHeight + 1)
                return;
            /* if we don't have transactions from block than request it */
            var haveNotTxs = _GetMissingTransactions(block);
            if (haveNotTxs.Count > 0)
            {
                var totalFound = WaitForTransactions(block.TransactionHashes, timeout);
                /* if peer can't provide all hashes from block than he might be a lier */
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

        public void HandlePeerHasBlocks(ulong blockHeight, IRemotePeer remotePeer)
        {
            lock (_peerHasBlocks)
            {
                if (_peerHeights.TryGetValue(remotePeer, out var peerHeight) && blockHeight <= peerHeight)
                    return;
                _peerHeights[remotePeer] = blockHeight;
                Monitor.PulseAll(_peerHasBlocks);
            }
        }

        public bool IsSynchronizingWith(IEnumerable<ECDSAPublicKey> peers)
        {
            if (_networkContext.LocalNode is null) throw new InvalidOperationException();
            var myHeight = _blockManager.GetHeight();
            if (myHeight > _networkContext.LocalNode.BlockHeight)
                _networkContext.LocalNode.BlockHeight = myHeight;
            var setOfPeers = peers.ToHashSet();
            if (setOfPeers.Count == 0) return false;

            _lastActiveTime = TimeUtils.CurrentTimeMillis();
            var messageFactory = _networkManager.MessageFactory ?? throw new InvalidOperationException();
            _networkBroadcaster.Broadcast(messageFactory.PingRequest(TimeUtils.CurrentTimeMillis(), myHeight));
            lock (_peerHasBlocks)
                Monitor.Wait(_peerHasBlocks, TimeSpan.FromSeconds(1));
            var validatorPeers = _peerHeights
                .Where(entry => entry.Key.Node != null)
                .Where(entry => setOfPeers.Contains(entry.Key.Node?.PublicKey!))
                .ToArray();
            if (validatorPeers.Length < setOfPeers.Count * 2 / 3)
                return true;
            var maxHeight = validatorPeers.Max(v => v.Value);
            return myHeight < maxHeight;
        }

        public void SynchronizeWith(IEnumerable<ECDSAPublicKey> peers)
        {
            var peersArray = peers.ToArray();
            while (IsSynchronizingWith(peersArray))
            {
                Thread.Sleep(TimeSpan.FromMilliseconds(1_000));
            }
        }

        public PeerAddress[] GetConnectedPeers()
        {
            var connectedPeerAddresses = _peerHeights.Where(peer => peer.Key.IsConnected)
                .Select(peer => peer.Key.Address).ToArray();
            return connectedPeerAddresses;
        }

        public ulong? GetHighestBlock()
        {
            var validatorPeers = _peerHeights
                .Where(entry => entry.Key.Node != null)
                .ToArray();
            if (validatorPeers.Length == 0) return null;

            return validatorPeers.Max(v => v.Value);
        }

        private ulong _lastActiveTime = TimeUtils.CurrentTimeMillis();

        private void _Worker()
        {
            if (_networkContext.LocalNode is null) throw new InvalidOperationException();
            var myHeight = _blockManager.GetHeight();
            if (myHeight > _networkContext.LocalNode.BlockHeight)
                _networkContext.LocalNode.BlockHeight = myHeight;

            if (_networkContext.ActivePeers.Values.Count == 0)
                return;

            var messageFactory = _networkManager.MessageFactory ?? throw new InvalidOperationException();
            _networkBroadcaster.Broadcast(messageFactory.PingRequest(TimeUtils.CurrentTimeMillis(), myHeight));
            lock (_peerHasBlocks)
                Monitor.Wait(_peerHasBlocks, TimeSpan.FromSeconds(1));
            if (_peerHeights.Count == 0)
                return;

            var maxHeight = _peerHeights.Values.Max(v => v);
            if (myHeight >= maxHeight)
                return;

            var peers = _peerHeights.Where(entry => entry.Value == maxHeight).Select(entry => entry.Key);

            foreach (var peer in peers)
                peer.Send(messageFactory.GetBlocksByHeightRangeRequest(myHeight + 1, maxHeight));

            lock (_peerHasBlocks)
                Monitor.Wait(_peerHasBlocks, TimeSpan.FromSeconds(1));
        }

        public void Start()
        {
            Task.Factory.StartNew(() =>
            {
                Thread.Sleep(15_000);
                try
                {
                    var thread = Thread.CurrentThread;
                    while (thread.IsAlive)
                    {
                        _Worker();
                        Thread.Sleep(5000);
                    }
                }
                catch (Exception e)
                {
                    Console.Error.WriteLine(e);
                    Logger.LogError(e.Message);
                }
            }, TaskCreationOptions.LongRunning);
        }

        private List<UInt256> _GetMissingTransactions(IEnumerable<UInt256> txHashes)
        {
            var list = new List<UInt256>();
            foreach (var hash in txHashes)
            {
                var tx = _transactionManager.GetByHash(hash) ?? _transactionPool.GetByHash(hash);
                if (tx != null)
                    continue;
                list.Add(hash);
            }

            return list;
        }

        private List<UInt256> _GetMissingTransactions(Block block)
        {
            return _GetMissingTransactions(block.TransactionHashes);
        }
    }
}