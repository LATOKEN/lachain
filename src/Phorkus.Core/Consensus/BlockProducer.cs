using System;
using System.Collections.Generic;
using System.Linq;
using Phorkus.Core.Blockchain;
using Phorkus.Core.Blockchain.OperationManager;
using Phorkus.Core.Network;
using Phorkus.Logger;
using Phorkus.Proto;
using Phorkus.Utility.Utils;

namespace Phorkus.Core.Consensus
{
    public class BlockProducer : IBlockProducer
    {
        private readonly ILogger<BlockProducer> _logger = LoggerFactory.GetLoggerForClass<BlockProducer>();
        private readonly ITransactionPool _transactionPool;
        private readonly IValidatorManager _validatorManager;
        private readonly IBlockchainContext _blockchainContext;
        private readonly IBlockSynchronizer _blockSynchronizer;
        private readonly IBlockManager _blockManager;
        private const int BatchSize = 100; // TODO: calculate batch size
        private readonly Random _random = new Random();

        public BlockProducer(
            ITransactionPool transactionPool,
            IValidatorManager validatorManager,
            IBlockchainContext blockchainContext,
            IBlockSynchronizer blockSynchronizer, IBlockManager blockManager)
        {
            _transactionPool = transactionPool;
            _validatorManager = validatorManager;
            _blockchainContext = blockchainContext;
            _blockSynchronizer = blockSynchronizer;
            _blockManager = blockManager;
        }

        public IEnumerable<TransactionReceipt> GetTransactionsToPropose()
        {
            var txNum = (BatchSize + _validatorManager.Validators.Count - 1) / _validatorManager.Validators.Count;
            var allTxs = _transactionPool.Transactions.Values.ToArray();
            _random.Shuffle(allTxs);
            return allTxs.Take(txNum);
        }

        public void ProduceBlock(
            IReadOnlyCollection<UInt256> txHashes, ECDSAPublicKey publicKey, ulong nonce
        )
        {
            var txsGot = _blockSynchronizer.WaitForTransactions(txHashes, TimeSpan.FromDays(1)); // TODO: timeout?
            if (txsGot != txHashes.Count)
            {
                _logger.LogError(
                    $"Cannot retrieve all transactions in time, got only {txsGot} of {txHashes.Count}, aborting");
                throw new InvalidOperationException(
                    $"Cannot retrieve all transactions in time, got only {txsGot} of {txHashes.Count}, aborting");
            }

            var receipts = txHashes
                .Select(hash => _transactionPool.GetByHash(hash) ?? throw new InvalidOperationException())
                .ToList();

            var blockWithTransactions =
                new BlockBuilder(
                        _blockchainContext.CurrentBlock?.Header ?? throw new InvalidOperationException(),
                        publicKey)
                    .WithTransactions(receipts)
                    .Build(nonce);

            var (operatingError, removeTransactions, stateHash, relayTransactions) =
                _blockManager.Emulate(blockWithTransactions.Block, blockWithTransactions.Transactions);

            if (operatingError != OperatingError.Ok)
                throw new InvalidOperationException($"Cannot assemble block: error {operatingError}");

            blockWithTransactions = new BlockBuilder(_blockchainContext.CurrentBlock.Header, publicKey, stateHash)
                .WithTransactions(receipts)
                .Build(nonce);

            _logger.LogInformation($"Block approved by consensus: {blockWithTransactions.Block.Hash}");
            var result = _blockManager.Execute(
                blockWithTransactions.Block, blockWithTransactions.Transactions, commit: true, checkStateHash: true);

            if (result == OperatingError.Ok)
                _logger.LogInformation($"Block persist completed: {blockWithTransactions.Block.Hash}");
            else
                _logger.LogError(
                    $"Block {blockWithTransactions.Block.Header.Index} hasn't been persisted: {blockWithTransactions.Block.Hash}, cuz error {result}");
        }
    }
}