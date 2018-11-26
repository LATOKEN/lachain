using System;
using System.Collections.Generic;
using Phorkus.Core.Blockchain;
using Phorkus.Core.Blockchain.OperationManager;
using Phorkus.Core.Logging;
using Phorkus.Proto;

namespace Phorkus.Core.Network
{
    public class Synchronizer : ISynchronizer
    {
        private readonly IBlockchainService _blockchainService;
        private readonly ITransactionManager _transactionManager;
        private readonly IBlockManager _blockManager;
        private readonly IBlockchainContext _blockchainContext;
        private readonly ILogger<ISynchronizer> _logger;

        public Synchronizer(
            IBlockchainService blockchainService,
            ITransactionManager transactionManager,
            IBlockManager blockManager,
            IBlockchainContext blockchainContext,
            ILogger<ISynchronizer> logger)
        {
            _blockchainService = blockchainService;
            _transactionManager = transactionManager;
            _blockManager = blockManager;
            _blockchainContext = blockchainContext;
            _logger = logger;
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
            var txs = _blockchainService.GetTransactionsByHashes(transactionHashes);
            var persisted = 0u;
            foreach (var tx in txs)
            {
                var error = _transactionManager.Persist(tx);
                if (error != OperatingError.Ok)
                    continue;
                ++persisted;
            }
            return persisted;
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
                _logger.LogWarning($"Unable to persist block {block.Header.Index}, got error {error}, dropping peer");
                return;
            }
            _logger.LogInformation($"Synchronized block {block.Header.Index} with hash {block.Hash}");
        }

        public void Start()
        {
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