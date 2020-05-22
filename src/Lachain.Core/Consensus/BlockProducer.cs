using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Principal;
using Google.Protobuf;
using Lachain.Logger;
using Lachain.Consensus;
using Lachain.Core.Blockchain;
using Lachain.Core.Blockchain.Error;
using Lachain.Core.Blockchain.Interface;
using Lachain.Core.Blockchain.Operations;
using Lachain.Core.Blockchain.Pool;
using Lachain.Core.Blockchain.SystemContracts;
using Lachain.Core.Blockchain.SystemContracts.ContractManager;
using Lachain.Core.Blockchain.SystemContracts.Interface;
using Lachain.Core.Blockchain.Validators;
using Lachain.Core.Blockchain.VM;
using Lachain.Core.Network;
using Lachain.Crypto;
using Lachain.Proto;
using Lachain.Storage.State;
using Lachain.Utility.Utils;

namespace Lachain.Core.Consensus
{
    public class BlockProducer : IBlockProducer
    {
        private readonly ILogger<BlockProducer> _logger = LoggerFactory.GetLoggerForClass<BlockProducer>();
        private readonly ITransactionPool _transactionPool;
        private readonly IValidatorManager _validatorManager;
        private readonly IBlockchainContext _blockchainContext;
        private readonly IBlockSynchronizer _blockSynchronizer;
        private readonly IBlockManager _blockManager;
        private readonly ITransactionBuilder _transactionBuilder;
        private readonly IStateManager _stateManager;
        private const int BatchSize = 1000; // TODO: calculate batch size

        public BlockProducer(
            ITransactionPool transactionPool,
            IValidatorManager validatorManager,
            IBlockchainContext blockchainContext,
            IBlockSynchronizer blockSynchronizer,
            IBlockManager blockManager,
            IStateManager stateManager,
            ITransactionBuilder transactionBuilder
        )
        {
            _transactionPool = transactionPool;
            _validatorManager = validatorManager;
            _blockchainContext = blockchainContext;
            _blockSynchronizer = blockSynchronizer;
            _blockManager = blockManager;
            _stateManager = stateManager;
            _transactionBuilder = transactionBuilder;
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

            var cycle = index / StakingContract.CycleDuration;
            var indexInCycle = index % StakingContract.CycleDuration;
            if (cycle > 0 && indexInCycle == 100)
            {
                receipts = receipts.Concat(new[] {DistributeCycleRewardsAndPenaltiesTxReceipt()}).ToList();
            } 
            else if (indexInCycle == 500)
            {
                receipts = receipts.Concat(new[] {FinishVrfLotteryTxReceipt()}).ToList();   
            }
            else if (cycle > 0 && indexInCycle == 0)
            {
                receipts = receipts.Concat(new[] {FinishCycleTxReceipt()}).ToList();   
            }

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
            var hashes = txHashes as UInt256[] ?? txHashes.ToArray();
            var txCount = hashes.Count();
            var indexInCycle = header.Index % 1000;
            var cycle = header.Index / 1000;
            var receipts = hashes
                .Select((hash, i) =>
                {
                    if (cycle > 0 && indexInCycle == 100 && i == txCount - 1)
                        return DistributeCycleRewardsAndPenaltiesTxReceipt();
                    if (indexInCycle == 500 && i == txCount - 1)
                        return FinishVrfLotteryTxReceipt();
                    if (cycle > 0 && indexInCycle == 0 && i == txCount - 1)
                        return FinishCycleTxReceipt();
                    return _transactionPool.GetByHash(hash) ?? throw new InvalidOperationException();
                    
                })
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
                    $"Block {blockWithTransactions.Block.Header.Index} ({blockWithTransactions.Block.Hash.ToHex()}) was not persisted: {result}");
        }

        private TransactionReceipt DistributeCycleRewardsAndPenaltiesTxReceipt(int nonceInc = 0)
        {
            return BuildSystemContractTxReceipt(ContractRegisterer.GovernanceContract,
                GovernanceInterface.MethodDistributeCycleRewardsAndPenalties, nonceInc);
        }

        private TransactionReceipt FinishVrfLotteryTxReceipt(int nonceInc = 0)
        {
            return BuildSystemContractTxReceipt(ContractRegisterer.StakingContract,
                StakingInterface.MethodFinishVrfLottery, nonceInc);
        }

        private TransactionReceipt FinishCycleTxReceipt(int nonceInc = 0)
        {
            return BuildSystemContractTxReceipt(ContractRegisterer.GovernanceContract,
                GovernanceInterface.MethodFinishCycle, nonceInc);
        }

        private TransactionReceipt BuildSystemContractTxReceipt(UInt160 contractAddress, string mehodSignature, int nonceInc)
        {
            var nonce = _stateManager.LastApprovedSnapshot.Transactions.GetTotalTransactionCount(UInt160Utils.Zero) + (ulong) nonceInc;
            var abi = ContractEncoder.Encode(mehodSignature);
            var transaction = new Transaction
            {
                To = contractAddress,
                Value = UInt256Utils.Zero,
                From = UInt160Utils.Zero,
                Nonce = nonce,
                GasPrice = 0,
                /* TODO: gas estimation */
                GasLimit = 100000000,
                Invocation = ByteString.CopyFrom(abi),
            };
            return new TransactionReceipt
            {
                Hash = transaction.FullHash(SignatureUtils.Zero),
                Status = TransactionStatus.Pool,
                Transaction = transaction,
                Signature = SignatureUtils.Zero,
            };
        }
    }
}