using System;
using System.Collections.Generic;
using System.Linq;
using Lachain.Logger;
using Lachain.Consensus;
using Lachain.Core.Blockchain.Error;
using Lachain.Core.Blockchain.Interface;
using Lachain.Core.Blockchain.Pool;
using Lachain.Core.Blockchain.Validators;
using Lachain.Core.Network;
using Lachain.Proto;
using Lachain.Utility.Utils;

namespace Lachain.Core.Consensus
{
    public class BlockProducer : IBlockProducer
    {
        private readonly ILogger<BlockProducer> _logger = LoggerFactory.GetLoggerForClass<BlockProducer>();
        private readonly ITransactionPool _transactionPool;
        private readonly IValidatorManager _validatorManager;
        private readonly IBlockSynchronizer _blockSynchronizer;
        private readonly IBlockManager _blockManager;
        private const int BatchSize = 1000; // TODO: calculate batch size

        public BlockProducer(
            ITransactionPool transactionPool,
            IValidatorManager validatorManager,
            IBlockSynchronizer blockSynchronizer,
            IBlockManager blockManager
        )
        {
            _transactionPool = transactionPool;
            _validatorManager = validatorManager;
            _blockSynchronizer = blockSynchronizer;
            _blockManager = blockManager;
        }

        public IEnumerable<TransactionReceipt> GetTransactionsToPropose(long era)
        {
            var n = _validatorManager.GetValidators(era - 1).N;
            var txNum = (BatchSize + n - 1) / n;
            var taken = _transactionPool.Peek(BatchSize, txNum);
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

            if (_blockManager.LatestBlock().Header.Index + 1 != index)
            {
                throw new InvalidOperationException(
                    $"Latest block is {_blockManager.LatestBlock()}, but we are trying to create block {index}");
            }

            var blockWithTransactions =
                new BlockBuilder(_blockManager.LatestBlock().Header)
                    .WithTransactions(receipts)
                    .Build(nonce);

            var (operatingError, removedTxs, stateHash, returnedTxs) =
                _blockManager.Emulate(blockWithTransactions.Block, blockWithTransactions.Transactions);

            var badHashes = new HashSet<UInt256>(
                removedTxs.Select(receipt => receipt.Hash).Concat(returnedTxs.Select(receipt => receipt.Hash))
            );

            blockWithTransactions = new BlockBuilder(_blockManager.LatestBlock().Header)
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

            var blockWithTransactions = new BlockBuilder(_blockManager.LatestBlock().Header, header.StateHash)
                .WithTransactions(receipts)
                .WithMultisig(multiSig)
                .Build(header.Nonce);

            _logger.LogInformation($"Block approved by consensus: {blockWithTransactions.Block.Hash.ToHex()}");
            if (_blockManager.GetHeight() + 1 != header.Index)
            {
                throw new InvalidOperationException(
                    $"Current height is {_blockManager.GetHeight()}, but we are trying to produce block {header.Index}"
                );
            }

            var result = _blockManager.Execute(
                blockWithTransactions.Block, blockWithTransactions.Transactions, commit: true,
                checkStateHash: true);

            if (result == OperatingError.Ok)
                _logger.LogInformation($"Block persist completed: {blockWithTransactions.Block.Hash.ToHex()}");
            else
                _logger.LogError(
                    $"Block {blockWithTransactions.Block.Header.Index} ({blockWithTransactions.Block.Hash.ToHex()}) was not persisted: {result}");
        }
    }
}