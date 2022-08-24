using System;
using System.Collections.Generic;
using System.Linq;
using Google.Protobuf;
using Lachain.Logger;
using Lachain.Consensus;
using Lachain.Core.Blockchain.Error;
using Lachain.Core.Blockchain.Hardfork;
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
    /*
        BlockProducer object handles the following tasks.
        
        (1) Transaction Proposal: After consensus starts for an era, a validator node needs a set of
        proposed transactions. BlockProducer acts as an intermediate entity between consensus and pool, 
        fetches the proposed set from the pool and gives it to RootProtocol during consensus. 

        (2) Block Emulation and create header: After consensus, the set of transactions is selected for
        next block. BlockProducer emulates the transactions, removes bad transactions, calculate state hash
        and finally creates the block header. 

        (3) Block Execution and Produce Block: After the emulated stateHash is approved by sufficient 
        number of validators, it is executed and a new block is thus produced.  
    */
    public class BlockProducer : IBlockProducer
    {
        private static readonly ILogger<BlockProducer> Logger = LoggerFactory.GetLoggerForClass<BlockProducer>();
        private readonly ITransactionPool _transactionPool;
        private readonly IValidatorManager _validatorManager;
        private readonly IBlockSynchronizer _blockSynchronizer;
        private readonly IBlockManager _blockManager;
        private readonly IStateManager _stateManager;
        private readonly ITransactionBuilder _transactionBuilder;
        private readonly ITransactionVerifier _transactionVerifier;
        private const int BatchSize = 1000; // TODO: calculate batch size

        public BlockProducer(
            ITransactionVerifier transactionVerifier,
            ITransactionPool transactionPool,
            IValidatorManager validatorManager,
            IBlockSynchronizer blockSynchronizer,
            IBlockManager blockManager,
            IStateManager stateManager, 
            ITransactionBuilder transactionBuilder
        )
        {
            _transactionVerifier = transactionVerifier;
            _transactionPool = transactionPool;
            _validatorManager = validatorManager;
            _blockSynchronizer = blockSynchronizer;
            _blockManager = blockManager;
            _stateManager = stateManager;
            _transactionBuilder = transactionBuilder;
        }

        // Given an era, returns a proposed set of transaction receipts
        public IEnumerable<TransactionReceipt> GetTransactionsToPropose(long era)
        {
        
            var n = _validatorManager.GetValidators(era - 1)!.N;
            var txNum = (BatchSize + n - 1) / n;
            if(era < 0)
            {
                Logger.LogError($"era : {era} should not be negative");
                throw new ArgumentException("era is negative");
            }
            // the number of transactions in a block is controlled by this "BatchSize" 
            // variable. Every validator considers BatchSize number of transactions 
            // and takes ceil(BatchSize / validatorCount) number of transactions. If the
            // transactions are selected randomly, then the expected number of transactions
            // in a block is BatchSize.
            var taken = _transactionPool.Peek(BatchSize, txNum, (ulong) era);
            Logger.LogTrace($"Proposed Transactions Count: {taken.Count()}");
            return taken;
        }

        // Taking the selected transaction receipts from consensus, CreateHeader removes bad
        // transactions, adds necessary system transactions, emulate the transactions, calculate
        // stateHash and finally returns a blockHeader
        public BlockHeader? CreateHeader(
            ulong index, IReadOnlyCollection<TransactionReceipt> receipts, ulong nonce, out TransactionReceipt[] receiptsTaken
        )
        {
            Logger.LogTrace("CreateHeader");
            if (_blockManager.GetHeight() >= index)
            {
                Logger.LogWarning("Block already produced");
                receiptsTaken = new TransactionReceipt[]{};
                return null;
            }

            // we don't need to verify receipts here
            // verfification will be done during emulation

            // But we need to verify the hash as we map the receipts with its hash
            // we skip the transactions with hash mismatch
            receipts = receipts.Where(receipt => 
                receipt.Transaction.FullHash(receipt.Signature,  HardforkHeights.IsHardfork_9Active(index)).Equals(receipt.Hash)).ToList();

            receipts = receipts.OrderBy(receipt => receipt, new ReceiptComparer())
                .ToList();

            // Add receipts to _transactionVerifier to verify asynchronously
            _transactionVerifier.VerifyTransactions(receipts, HardforkHeights.IsHardfork_9Active(index));
            
            var cycle = index / StakingContract.CycleDuration;
            var indexInCycle = index % StakingContract.CycleDuration;

            // try to add necessary system transactions at the end
            if (cycle > 0 && indexInCycle == StakingContract.AttendanceDetectionDuration)
            {
                var txToAdd = DistributeCycleRewardsAndPenaltiesTxReceipt(index);
                if (receipts.Select(x => x.Hash).Contains(txToAdd.Hash))
                    Logger.LogDebug("DistributeCycleRewardsAndPenaltiesTxReceipt is already in txPool");
                else receipts = receipts.Concat(new[] {txToAdd}).ToList();
            }
            else if (indexInCycle == StakingContract.VrfSubmissionPhaseDuration)
            {
                var txToAdd = FinishVrfLotteryTxReceipt(index);
                if (receipts.Select(x => x.Hash).Contains(txToAdd.Hash))
                    Logger.LogDebug("FinishVrfLotteryTxReceipt is already in txPool");
                else receipts = receipts.Concat(new[] {txToAdd}).ToList();
            }
            else if (cycle > 0 && indexInCycle == 0)
            {
                var txToAdd = FinishCycleTxReceipt(index);
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

            
            var badReceipts = new HashSet<TransactionReceipt>(removedTxs.Concat(returnedTxs));
            receiptsTaken = receipts.Where(receipt => !badReceipts.Contains(receipt)).ToArray();

            blockWithTransactions = new BlockBuilder(_blockManager.LatestBlock().Header)
                .WithTransactions(receiptsTaken)
                .Build(nonce);

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

        // After sufficient votes are on the emulated stateHash, the block is approved and
        // executed to add to the chain
        public void ProduceBlock(IEnumerable<TransactionReceipt> receipts, BlockHeader header, MultiSig multiSig)
        {
            Logger.LogDebug($"Producing block {header.Index}");
            if (_blockManager.GetHeight() >= header.Index)
            {
                Logger.LogWarning("Block already produced");
                return;
            }
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

        private TransactionReceipt DistributeCycleRewardsAndPenaltiesTxReceipt(ulong blockIndex)
        {
            var tx = _transactionBuilder.InvokeTransactionWithGasPrice(
                UInt160Utils.Zero,
                ContractRegisterer.GovernanceContract,
                Utility.Money.Zero,
                GovernanceInterface.MethodDistributeCycleRewardsAndPenalties,
                0,
                UInt256Utils.ToUInt256((GovernanceContract.GetCycleByBlockNumber(_blockManager.GetHeight())))
            );
            return HardforkHeights.IsHardfork_9Active(blockIndex) ?
                new TransactionReceipt
                {
                    Hash = tx.FullHash(SignatureUtils.ZeroNew, true),
                    Status = TransactionStatus.Pool,
                    Transaction = tx,
                    Signature = SignatureUtils.ZeroNew,
                }
                :
                new TransactionReceipt
                {
                    Hash = tx.FullHash(SignatureUtils.ZeroOld, false),
                    Status = TransactionStatus.Pool,
                    Transaction = tx,
                    Signature = SignatureUtils.ZeroOld,
                };
        }

        private TransactionReceipt FinishVrfLotteryTxReceipt(ulong blockIndex)
        {
            return BuildSystemContractTxReceipt(blockIndex, ContractRegisterer.StakingContract,
                StakingInterface.MethodFinishVrfLottery);
        }

        private TransactionReceipt FinishCycleTxReceipt(ulong blockIndex)
        {
            var tx = _transactionBuilder.InvokeTransactionWithGasPrice(
                UInt160Utils.Zero,
                ContractRegisterer.GovernanceContract,
                Utility.Money.Zero,
                GovernanceInterface.MethodFinishCycle,
                0,
                UInt256Utils.ToUInt256(GovernanceContract.GetCycleByBlockNumber(_blockManager.GetHeight()))
            );
            return HardforkHeights.IsHardfork_9Active(blockIndex) ?
                new TransactionReceipt
                {
                    Hash = tx.FullHash(SignatureUtils.ZeroNew, true),
                    Status = TransactionStatus.Pool,
                    Transaction = tx,
                    Signature = SignatureUtils.ZeroNew,
                }
                :
                new TransactionReceipt
                {
                    Hash = tx.FullHash(SignatureUtils.ZeroOld, false),
                    Status = TransactionStatus.Pool,
                    Transaction = tx,
                    Signature = SignatureUtils.ZeroOld,
                };
        }

        private TransactionReceipt BuildSystemContractTxReceipt(ulong blockIndex, UInt160 contractAddress, string methodSignature)
        {
            var nonce = _stateManager.LastApprovedSnapshot.Transactions.GetTotalTransactionCount(UInt160Utils.Zero);
            var abi = ContractEncoder.Encode(methodSignature);
            var tx = new Transaction
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
            return HardforkHeights.IsHardfork_9Active(blockIndex) ?
                new TransactionReceipt
                {
                    Hash = tx.FullHash(SignatureUtils.ZeroNew, true),
                    Status = TransactionStatus.Pool,
                    Transaction = tx,
                    Signature = SignatureUtils.ZeroNew,
                }
                :
                new TransactionReceipt
                {
                    Hash = tx.FullHash(SignatureUtils.ZeroOld, false),
                    Status = TransactionStatus.Pool,
                    Transaction = tx,
                    Signature = SignatureUtils.ZeroOld,
                };
        }
    }
}