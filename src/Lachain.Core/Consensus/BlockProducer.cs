using System;
using System.Collections.Generic;
using System.Linq;
using Google.Protobuf;
using Lachain.Logger;
using Lachain.Consensus;
using Lachain.Core.Blockchain.Error;
using Lachain.Core.Blockchain.Interface;
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
        private static readonly ILogger<BlockProducer> Logger = LoggerFactory.GetLoggerForClass<BlockProducer>();
        private readonly ITransactionPool _transactionPool;
        private readonly IValidatorManager _validatorManager;
        private readonly IBlockSynchronizer _blockSynchronizer;
        private readonly IBlockManager _blockManager;
        private readonly IStateManager _stateManager;
        private readonly ITransactionBuilder _transactionBuilder;
        private const int BatchSize = 1000; // TODO: calculate batch size

        public BlockProducer(
            ITransactionPool transactionPool,
            IValidatorManager validatorManager,
            IBlockSynchronizer blockSynchronizer,
            IBlockManager blockManager,
            IStateManager stateManager, 
            ITransactionBuilder transactionBuilder
        )
        {
            _transactionPool = transactionPool;
            _validatorManager = validatorManager;
            _blockSynchronizer = blockSynchronizer;
            _blockManager = blockManager;
            _stateManager = stateManager;
            _transactionBuilder = transactionBuilder;
        }

        public IEnumerable<TransactionReceipt> GetTransactionsToPropose(long era)
        {
            var n = _validatorManager.GetValidators(era - 1)!.N;
            var txNum = (BatchSize + n - 1) / n;
            var taken = _transactionPool.Peek(BatchSize, txNum);
            return taken;
        }

        public BlockHeader? CreateHeader(
            ulong index, IReadOnlyCollection<UInt256> txHashes, ulong nonce, out UInt256[] hashesTaken
        )
        {
            if (_blockManager.GetHeight() >= index)
            {
                Logger.LogWarning("Block already produced");
                hashesTaken = new UInt256[]{};
                return null;
            }
            var txsGot = _blockSynchronizer.WaitForTransactions(txHashes, TimeSpan.FromHours(1), out List<TransactionReceipt> receipts); // TODO: timeout?
            if (txsGot != txHashes.Count)
            {
                Logger.LogError(
                    $"Cannot retrieve all transactions in time, got only {txsGot} of {txHashes.Count}, aborting");
                throw new InvalidOperationException(
                    $"Cannot retrieve all transactions in time, got only {txsGot} of {txHashes.Count}, aborting");
            }

            receipts = receipts.OrderBy(receipt => receipt, new ReceiptComparer())
                .ToList();

            var cycle = index / StakingContract.CycleDuration;
            var indexInCycle = index % StakingContract.CycleDuration;
            if (cycle > 0 && indexInCycle == StakingContract.AttendanceDetectionDuration)
            {
                var txToAdd = DistributeCycleRewardsAndPenaltiesTxReceipt();
                if (receipts.Select(x => x.Hash).Contains(txToAdd.Hash))
                    Logger.LogDebug("DistributeCycleRewardsAndPenaltiesTxReceipt is already in txPool");
                else receipts = receipts.Concat(new[] {txToAdd}).ToList();
            }
            else if (indexInCycle == StakingContract.VrfSubmissionPhaseDuration)
            {
                var txToAdd = FinishVrfLotteryTxReceipt();
                if (receipts.Select(x => x.Hash).Contains(txToAdd.Hash))
                    Logger.LogDebug("FinishVrfLotteryTxReceipt is already in txPool");
                else receipts = receipts.Concat(new[] {txToAdd}).ToList();
            }
            else if (cycle > 0 && indexInCycle == 0)
            {
                var txToAdd = FinishCycleTxReceipt();
                if (receipts.Select(x => x.Hash).Contains(txToAdd.Hash))
                    Logger.LogDebug("FinishCycleTxReceipt is already in txPool");
                else receipts = receipts.Concat(new[] {txToAdd}).ToList();
            }

            if (_blockManager.LatestBlock().Header.Index + 1 != index)
            {
                throw new InvalidOperationException(
                    $"Latest block is {_blockManager.LatestBlock().Header.Index} " +
                    $"with hash {_blockManager.LatestBlock().Hash.ToHex()}, " +
                    $"but we are trying to create block {index}");
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
            Logger.LogDebug($"Producing block {header.Index}");
            if (_blockManager.GetHeight() >= header.Index)
            {
                Logger.LogWarning("Block already produced");
                return;
            }

            var hashes = txHashes as UInt256[] ?? txHashes.ToArray();
            var txCount = hashes.Count();
            var indexInCycle = header.Index % StakingContract.CycleDuration;
            var cycle = header.Index / StakingContract.CycleDuration;
            var receipts = hashes
                .Select((hash, i) =>
                {
                    if (cycle > 0 && indexInCycle == StakingContract.AttendanceDetectionDuration && i == txCount - 1)
                        return DistributeCycleRewardsAndPenaltiesTxReceipt();
                    if (indexInCycle == StakingContract.VrfSubmissionPhaseDuration && i == txCount - 1)
                        return FinishVrfLotteryTxReceipt();
                    if (cycle > 0 && indexInCycle == 0 && i == txCount - 1)
                        return FinishCycleTxReceipt();
                    return _transactionPool.GetByHash(hash) ?? throw new InvalidOperationException();
                })
                .OrderBy(receipt => receipt, new ReceiptComparer())
                .ToList();

            var blockWithTransactions = new BlockBuilder(_blockManager.LatestBlock().Header, header.StateHash)
                .WithTransactions(receipts)
                .WithMultisig(multiSig)
                .Build(header.Nonce);

            Logger.LogDebug($"Block approved by consensus: {blockWithTransactions.Block.Hash.ToHex()}");
            if (_blockManager.GetHeight() + 1 != header.Index)
            {
                throw new InvalidOperationException(
                    $"Current height is {_blockManager.GetHeight()}, but we are trying to produce block {header.Index}"
                );
            }

            var result = _blockManager.Execute(
                blockWithTransactions.Block, blockWithTransactions.Transactions, commit: true,
                checkStateHash: true);

            if (result != OperatingError.Ok)
            {
                Logger.LogError(
                    $"Block {blockWithTransactions.Block.Header.Index} ({blockWithTransactions.Block.Hash.ToHex()}) was not persisted: {result}"
                );
                Logger.LogTrace($"Block raw data: {blockWithTransactions.Block.ToByteArray().ToHex()}");
                Logger.LogTrace($"Block transactions data: {string.Join(", ", blockWithTransactions.Transactions.Select(tx => tx.ToByteArray().ToHex()))}");
            }
        }

        private TransactionReceipt DistributeCycleRewardsAndPenaltiesTxReceipt()
        {
            var tx = _transactionBuilder.InvokeTransactionWithGasPrice(
                UInt160Utils.Zero,
                ContractRegisterer.GovernanceContract,
                Utility.Money.Zero,
                GovernanceInterface.MethodDistributeCycleRewardsAndPenalties,
                0,
                UInt256Utils.ToUInt256((GovernanceContract.GetCycleByBlockNumber(_blockManager.GetHeight())))
            );
            return new TransactionReceipt
            {
                Hash = tx.FullHash(SignatureUtils.Zero),
                Status = TransactionStatus.Pool,
                Transaction = tx,
                Signature = SignatureUtils.Zero,
            };
        }

        private TransactionReceipt FinishVrfLotteryTxReceipt()
        {
            return BuildSystemContractTxReceipt(ContractRegisterer.StakingContract,
                StakingInterface.MethodFinishVrfLottery);
        }

        private TransactionReceipt FinishCycleTxReceipt()
        {
            var tx = _transactionBuilder.InvokeTransactionWithGasPrice(
                UInt160Utils.Zero,
                ContractRegisterer.GovernanceContract,
                Utility.Money.Zero,
                GovernanceInterface.MethodFinishCycle,
                0,
                UInt256Utils.ToUInt256(GovernanceContract.GetCycleByBlockNumber(_blockManager.GetHeight()))
            );
            return new TransactionReceipt
            {
                Hash = tx.FullHash(SignatureUtils.Zero),
                Status = TransactionStatus.Pool,
                Transaction = tx,
                Signature = SignatureUtils.Zero,
            };
        }

        private TransactionReceipt BuildSystemContractTxReceipt(UInt160 contractAddress, string mehodSignature)
        {
            var nonce = _stateManager.LastApprovedSnapshot.Transactions.GetTotalTransactionCount(UInt160Utils.Zero);
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