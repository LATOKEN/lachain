using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Phorkus.Core.Blockchain;
using Phorkus.Core.Blockchain.OperationManager;
using Phorkus.Logger;
using Phorkus.Networking;
using Phorkus.Proto;
using Phorkus.Utility.Utils;

namespace Phorkus.Core.Network
{
    public class BlockSynchronizer : IBlockSynchronizer
    {
        private readonly ITransactionManager _transactionManager;
        private readonly IBlockManager _blockManager;
        private readonly IBlockchainContext _blockchainContext;
        private readonly ILogger<IBlockSynchronizer> _logger;
        private readonly ITransactionVerifier _transactionVerifier;
        private readonly INetworkContext _networkContext;
        private readonly INetworkBroadcaster _networkBroadcaster;
        private readonly INetworkManager _networkManager;
        
        private readonly IDictionary<IRemotePeer, ulong> _peerHeights
            = new ConcurrentDictionary<IRemotePeer, ulong>();
        
        private readonly object _peerHasTransactions = new object();
        private readonly object _peerHasBlocks = new object();
        
        public BlockSynchronizer(
            ITransactionManager transactionManager,
            IBlockManager blockManager,
            IBlockchainContext blockchainContext,
            ILogger<IBlockSynchronizer> logger,
            ITransactionVerifier transactionVerifier,
            INetworkContext networkContext,
            INetworkBroadcaster networkBroadcaster,
            INetworkManager networkManager)
        {
            _transactionManager = transactionManager;
            _blockManager = blockManager;
            _blockchainContext = blockchainContext;
            _logger = logger;
            _networkContext = networkContext;
            _networkBroadcaster = networkBroadcaster;
            _networkManager = networkManager;
            _transactionVerifier = transactionVerifier;
        }

        public uint WaitForTransactions(IEnumerable<UInt256> transactionHashes, TimeSpan timeout)
        {
            var txHashes = transactionHashes as UInt256[] ?? transactionHashes.ToArray();
            var lostTxs = _GetMissingTransactions(txHashes);
            _networkBroadcaster.Broadcast(_networkManager.MessageFactory.GetTransactionsByHashesRequest(lostTxs));
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
        
        public uint HandleTransactionsFromPeer(IEnumerable<SignedTransaction> transactions, IRemotePeer remotePeer)
        {
            var persisted = 0u;
            foreach (var tx in transactions)
            {
                var error = _transactionManager.Persist(tx);
                if (error != OperatingError.Ok)
                {
                    _logger.LogWarning($"Unable to persist transaction, cuz ({error})");
                    continue;
                }
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
            var myHeight = _blockchainContext.CurrentBlockHeaderHeight;
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
            var error = _blockManager.Persist(block);
            if (error == OperatingError.BlockAlreadyExists)
                return;
            if (error != OperatingError.Ok)
            {
                _logger.LogWarning($"Unable to persist block {block.Header.Index} (current height {_blockchainContext.CurrentBlockHeight}), got error {error}, dropping peer");
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
        
        private void _Worker()
        {
            var myHeight = _blockchainContext.CurrentBlockHeaderHeight;
            if (myHeight > _networkContext.LocalNode.BlockHeight)
                _networkContext.LocalNode.BlockHeight = myHeight;
            
            if (_networkContext.ActivePeers.Values.Count == 0)
                return;

            var messageFactory = _networkManager.MessageFactory;
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
                    _logger.LogError(e.Message);
                }
            }, TaskCreationOptions.LongRunning);
        }

        private List<UInt256> _GetMissingTransactions(IEnumerable<UInt256> txHashes)
        {
            var list = new List<UInt256>();
            foreach (var hash in txHashes)
            {
                var tx = _transactionManager.GetByHash(hash);
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