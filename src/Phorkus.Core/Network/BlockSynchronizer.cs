using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Phorkus.Core.Blockchain;
using Phorkus.Core.Blockchain.OperationManager;
using Phorkus.Logger;
using Phorkus.Proto;

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

        private readonly object _peerHasBlocks
            = new object();                
        
        public BlockSynchronizer(
            ITransactionManager transactionManager,
            IBlockManager blockManager,
            IBlockchainContext blockchainContext,
            ILogger<IBlockSynchronizer> logger,
            ITransactionVerifier transactionVerifier,
            INetworkContext networkContext)
        {
            _transactionManager = transactionManager;
            _blockManager = blockManager;
            _blockchainContext = blockchainContext;
            _logger = logger;
            _networkContext = networkContext;
            _transactionVerifier = transactionVerifier;
        }

        public uint HandleTransactionsFromPeer(IEnumerable<SignedTransaction> transactions, IRemotePeer remotePeer)
        {
            var persisted = 0u;
            foreach (var tx in transactions)
            {
                var error = _transactionManager.Persist(tx);
                if (error != OperatingError.Ok)
                    continue;
                persisted++;
            }

            return persisted;
        }

        public uint WaitForTransactions(IEnumerable<UInt256> transactionHashes, TimeSpan timeout)
        {
//            var txs = _blockchainService.GetTransactionsByHashes(transactionHashes);
//            var persisted = 0u;
//            foreach (var tx in txs)
//            {
//                var error = _transactionManager.Persist(tx);
//                if (error != OperatingError.Ok)
//                    continue;
//                ++persisted;
//            }
//
//            return persisted;
            return 0;
        }

        public void HandleBlockFromPeer(Block block, IRemotePeer remotePeer)
        {
            var myHeight = _blockchainContext.CurrentBlockHeaderHeight;
            if (block.Header.Index <= myHeight)
                return;

            var haveNotTxs = _HaveTransactions(block);
            if (haveNotTxs.Count > 0)
            {
                var txs = remotePeer.BlockchainService.GetTransactionsByHashes(haveNotTxs);
                /* if peer can't provide all hashes from block than he might be a lier */
                if (HandleTransactionsFromPeer(txs, remotePeer) != haveNotTxs.Count)
                    return;
            }

            var error = _blockManager.Persist(block);
            if (error == OperatingError.BlockAlreadyExists)
                return;
            if (error != OperatingError.Ok)
            {
                _logger.LogWarning($"Unable to persist block {block.Header.Index} (current height {_blockchainContext.CurrentBlockHeight}), got error {error}, dropping peer");
                return;
            }

            _logger.LogInformation($"Synchronized block {block.Header.Index} with hash {block.Hash}");
        }

        public void HandlePeerHasBlocks(ulong blockHeight)
        {
            lock (_peerHasBlocks)
            {
                Monitor.PulseAll(_peerHasBlocks);
            }
        }

        public bool DownloadTransactions(IBlockchainService blockchainService, IEnumerable<UInt256> transactionHashes, TimeSpan timeout)
        {
            var dontHave = _HaveTransactions(transactionHashes);
            if (dontHave.Count == 0)
                return true;
            var txs = blockchainService.GetTransactionsByHashes(dontHave).ToArray();
            if (txs.Length != dontHave.Count)
                return false;
            foreach (var tx in txs)
                _transactionVerifier.VerifyTransaction(tx);
            foreach (var tx in txs)
            {
                var result = _transactionManager.Persist(tx);
                if (result == OperatingError.Ok)
                    continue;
                Console.WriteLine($"Unable to persist transaction {tx.Hash}, cuz error {result}");
            }
            return true;
        }

        private IEnumerable<Block> _DownloadTransactions(IEnumerable<UInt256> blockHashes, IRemotePeer peer)
        {
            var validBlocks = new List<Block>();
            var i = 0;
            var blocks = peer.BlockchainService.GetBlocksByHashes(blockHashes).ToArray();
            foreach (var block in blocks)
            {   
                if (++i % 1 == 0 && blocks.Length > 1)
                {
                    Console.Write($"Downloading transactions... {i}/{blocks.Length} ({100 * i / blocks.Length}%)");
                    Console.CursorLeft = 0;
                }
                var dontHave = _HaveTransactions(block);
                if (dontHave.Count == 0)
                    continue;
                var txs = peer.BlockchainService.GetTransactionsByHashes(dontHave).ToArray();
                if (txs.Length != dontHave.Count)
                    continue;
                foreach (var tx in txs)
                    _transactionVerifier.VerifyTransaction(tx);
                foreach (var tx in txs)
                {
                    var result = _transactionManager.Persist(tx);
                    if (result == OperatingError.Ok)
                        continue;
                    Console.WriteLine($"Unable to persist transaction {tx.Hash}, cuz error {result}");
                    /* TODO: "revert block here" */   
                }
                dontHave = _HaveTransactions(block);
                if (dontHave.Count > 0)
                    continue;
                validBlocks.Add(block);
            }
            return validBlocks;
        }
        
        private void _Worker()
        {
            var myHeight = _blockchainContext.CurrentBlockHeaderHeight;
            if (myHeight > _networkContext.LocalNode.BlockHeight)
                _networkContext.LocalNode.BlockHeight = myHeight;
            
            if (_networkContext.ActivePeers.Values.Count == 0)
                return;
            
            var handshake = new HandshakeRequest
            {
                Node = _networkContext.LocalNode
            };
            /* TODO: "this code should be optimized" */
            var maxHeight0 = _networkContext.ActivePeers.Values.Max(peer =>
            {
                var handshaken = peer.BlockchainService.Handshake(handshake);
                return handshaken is null ? 0 : (long) handshaken.Node.BlockHeight;
            });
            var maxHeight = (ulong) maxHeight0;
            
            if (myHeight >= maxHeight)
                return;

            var blockHashes = _networkContext.ActivePeers.Values.AsParallel()
                .Select(peer => peer.BlockchainService.GetBlocksHashesByHeightRange(myHeight + 1, maxHeight))
                .Aggregate((b1, b2) => b1.Concat(b2))
                .Distinct()
                .ToArray();
            
            var blocks = _networkContext.ActivePeers.Values
                .Select(peer => _DownloadTransactions(blockHashes, peer))
                .Aggregate((b1, b2) => b1.Concat(b2).ToArray())
                .ToDictionary(b => b.Hash);
            
            var ordered = blocks.Values.OrderBy(b => b.Header.Index).ToArray();
            foreach (var block in ordered)
            {
                var error = _blockManager.Persist(block);
                if (error == OperatingError.Ok || error == OperatingError.BlockAlreadyExists)
                    continue;
                _logger.LogWarning($"Unable to persist block {block.Header.Index} (current height {_blockchainContext.CurrentBlockHeight}), got error {error}, dropping peer");
            }
            
            lock (_peerHasBlocks)
            {
                Monitor.Wait(_peerHasBlocks, TimeSpan.FromSeconds(1));
            }
        }

        public void Start()
        {
            Task.Factory.StartNew(() =>
            {
                try
                {
                    var thread = Thread.CurrentThread;
                    while (thread.IsAlive)
                        _Worker();
                }
                catch (Exception e)
                {
                    _logger.LogError(e.Message);
                }
            }, TaskCreationOptions.LongRunning);
        }

        private List<UInt256> _HaveTransactions(IEnumerable<UInt256> txHashes)
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

        private List<UInt256> _HaveTransactions(Block block)
        {
            return _HaveTransactions(block.TransactionHashes);
        }
    }
}