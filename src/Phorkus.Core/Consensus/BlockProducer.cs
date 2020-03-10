using System;
using System.Collections.Generic;
using System.Linq;
using Google.Protobuf;
using Phorkus.Consensus;
using Phorkus.Core.Blockchain;
using Phorkus.Core.Blockchain.OperationManager;
using Phorkus.Core.Network;
using Phorkus.Crypto;
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
        private const int BatchSize = 1000; // TODO: calculate batch size
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
            var allTxs = _transactionPool.Peek(txNum);
            txNum = Math.Min(txNum, allTxs.Count);
            var groups = allTxs
                .GroupBy(receipt => receipt.Transaction.From)
                .ToDictionary(receipts => receipts.Key, receipts => receipts.Reverse().ToList());

            // TODO: we need more complex & robust mechanism to peek transactions
            var taken = new List<TransactionReceipt>();
            var rnd = new Random();
            for (var i = 0; i < txNum; ++i)
            {
                var key = rnd.SelectRandom(groups.Keys);
                var tx = groups[key][groups[key].Count - 1];
                taken.Add(tx);
                groups[key].RemoveAt(groups[key].Count - 1);
                if (groups[key].Count == 0) groups.Remove(key);
            }

            return taken;
        }

        public BlockHeader CreateHeader(
            ulong index, IReadOnlyCollection<UInt256> txHashes, ulong nonce, out UInt256[] hashesTaken
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
                .OrderBy(receipt => receipt, new ReceiptComparer())
                .ToList();

            if (_blockchainContext.CurrentBlock is null) throw new InvalidOperationException("No previous block");
            if (_blockchainContext.CurrentBlock.Header.Index + 1 != index)
            {
                throw new InvalidOperationException(
                    $"Latest block is {_blockchainContext.CurrentBlock}, but we are trying to create block {index}");
            }

            var blockWithTransactions =
                new BlockBuilder(_blockchainContext.CurrentBlock.Header)
                    .WithTransactions(receipts)
                    .Build(nonce);

            var (operatingError, removedTxs, stateHash, returnedTxs) =
                _blockManager.Emulate(blockWithTransactions.Block, blockWithTransactions.Transactions);

            var badHashes = new HashSet<UInt256>(
                removedTxs.Select(receipt => receipt.Hash).Concat(returnedTxs.Select(receipt => receipt.Hash))
            );

            blockWithTransactions = new BlockBuilder(_blockchainContext.CurrentBlock.Header)
                .WithTransactions(receipts.FindAll(receipt => !badHashes.Contains(receipt.Hash)).ToArray())
                .Build(nonce);

            hashesTaken = blockWithTransactions.Transactions.Select(receipt => receipt.Hash).ToArray();
            if (operatingError != OperatingError.Ok)
                throw new InvalidOperationException($"Cannot assemble block: error {operatingError}");

            return new BlockHeader
            {
                Index = blockWithTransactions.Block.Header.Index,
                MerkleRoot = blockWithTransactions.Block.Header.MerkleRoot,
                Nonce = nonce,
                PrevBlockHash = blockWithTransactions.Block.Header.PrevBlockHash,
                StateHash = stateHash
            };
        }

        public void ProduceBlock(IEnumerable<UInt256> txHashes, BlockHeader header, MultiSig multiSig)
        {
            var receipts = txHashes
                .Select(hash => _transactionPool.GetByHash(hash) ?? throw new InvalidOperationException())
                .OrderBy(receipt => receipt, new ReceiptComparer()) 
                .ToList();

            var blockWithTransactions =
                new BlockBuilder(
                        _blockchainContext.CurrentBlock?.Header ?? throw new InvalidOperationException(),
                        header.StateHash
                    )
                    .WithTransactions(receipts)
                    .WithMultisig(multiSig)
                    .Build(header.Nonce);

            _logger.LogInformation($"Block approved by consensus: {blockWithTransactions.Block.Hash.ToHex()}");
            if (_blockchainContext.CurrentBlockHeight + 1 != header.Index)
            {
                throw new InvalidOperationException(
                    $"Current height is {_blockchainContext.CurrentBlockHeight}, but we are trying to produce block {header.Index}"
                );
            }

            var result = _blockManager.Execute(
                blockWithTransactions.Block, blockWithTransactions.Transactions, commit: true,
                checkStateHash: true);

            if (result == OperatingError.Ok)
                _logger.LogInformation($"Block persist completed: {blockWithTransactions.Block.Hash.ToHex()}");
            else
                _logger.LogError(
                    $"Block {blockWithTransactions.Block.Header.Index} hasn't been persisted: {blockWithTransactions.Block.Hash}, cuz error {result}");
        }
    }
}