using System;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Lachain.Core.Blockchain.Error;
using Lachain.Core.Blockchain.Interface;
using Lachain.Core.Blockchain.SystemContracts;
using Lachain.Core.Blockchain.SystemContracts.ContractManager;
using Lachain.Core.Blockchain.VM;
using Lachain.Crypto;
using Lachain.Proto;
using Lachain.Storage.State;
using Lachain.Utility;
using Lachain.Utility.Utils;
using Google.Protobuf;
using Lachain.Core.Blockchain.Hardfork;
using Lachain.Core.Blockchain.SystemContracts.Interface;
using Lachain.Logger;



namespace Lachain.Core.Blockchain.Operations
{
    public class TransactionManager : ITransactionManager
    {
        private static readonly ILogger<TransactionManager> Logger =
            LoggerFactory.GetLoggerForClass<TransactionManager>();

        private readonly ITransactionVerifier _transactionVerifier;
        private readonly IStateManager _stateManager;
        private readonly TransactionExecuter _transactionExecuter;

        // _processedTransactions keeps all the processed transactions (Verified or VerficationFailed) for current era
        // transaction gets removed from _processedTransactions once the block is persisted
        private readonly ConcurrentDictionary<UInt256, TransactionStatus> _processedTransactions
            = new ConcurrentDictionary<UInt256, TransactionStatus>();

        public event EventHandler<InvocationContext>? OnSystemContractInvoked;
        public event EventHandler<TransactionReceipt>? OnTransactionFailed;
        public event EventHandler<TransactionReceipt>? OnTransactionExecuted;

        public TransactionManager(
            ITransactionVerifier transactionVerifier,
            IContractRegisterer contractRegisterer,
            IStateManager stateManager
        )
        {
            _stateManager = stateManager ?? throw new ArgumentNullException(nameof(stateManager));
            _transactionVerifier = transactionVerifier ?? throw new ArgumentNullException(nameof(transactionVerifier));
            _transactionExecuter = new TransactionExecuter(contractRegisterer);
            _transactionExecuter.OnSystemContractInvoked +=
                (sender, context) => OnSystemContractInvoked?.Invoke(sender, context);
            // once a transaction is verified asynchronously, it invokes OnTransacionVerified
            // and this adds the transaction to _processedTransactions
            transactionVerifier.OnVerificationCompleted += OnVerificationCompleted;
            transactionVerifier.OnVerificationStarted += OnVerificationStarted;
        }

        public void ClearProcessedTransactions()
        {
            lock (_processedTransactions)
            {
                _transactionVerifier.ClearQueue();
                _processedTransactions.Clear();
            }
        }

        private void OnVerificationStarted(object? sender, object? arg)
        {
            ClearProcessedTransactions();
        }

        private void OnVerificationCompleted(object? sender, 
            (TransactionReceipt tx, TransactionStatus status) txWithStatus)
        {
            var (tx, verificationStatus) = txWithStatus;
            lock (_processedTransactions)
            {
                _processedTransactions.TryAdd(tx.Hash, verificationStatus);
            }
        }

        public TransactionReceipt? GetByHash(UInt256 transactionHash)
        {
            return _stateManager.CurrentSnapshot.Transactions.GetTransactionByHash(transactionHash);
        }
        // BlockManager uses this method to execute a single transaction
        [MethodImpl(MethodImplOptions.Synchronized)]
        public OperatingError Execute(Block block, TransactionReceipt receipt, IBlockchainSnapshot snapshot)
        {
            var transactionRepository = _stateManager.CurrentSnapshot.Transactions;
            /* check transaction with this hash in database */
            if (transactionRepository.GetTransactionByHash(receipt.Hash) != null)
                return OperatingError.AlreadyExists;
            /* verify transaction */

            /* find if verification should be skipped for this transaction */
            var indexInCycle = block.Header.Index % StakingContract.CycleDuration;
            var cycle = block.Header.Index / StakingContract.CycleDuration;

            var isGenesisBlock = block.Header.Index == 0;
            var isDistributeCycleRewardsAndPenaltiesTx = IsDistributeCycleRewardsAndPenaltiesTx(block, receipt.Transaction);
            var isFinishVrfLotteryTx = IsFinishVrfLotteryTx(block, receipt.Transaction);
            var isFinishCycleTx = IsFinishCycleTx(block, receipt.Transaction);
            // there are some cases when transaction verification is skipped
            // (1) genesis block's transactions
            // (2) DistributeCycleRewardsAndPenalties transaction. This transaction is executed during block, 
            //          (a) indexInCycle( = blockHeight % CycleDuration) == AttendanceDetectionDuration (CycleDuration / 10)
            // (3) FinishVrfLottery transaction. This transaction is executed during block, 
            //          (a) indexInCycle( = blockHeight % CycleDuration) == VrfSubmissionPhaseDuration (CycleDuration / 2)
            // (4) FinishCycle transaction. This transaction is executed during block, 
            //          (a) indexInCycle( = blockHeight % CycleDuration) == 0
            //          (b) cycle (= blockHeight / cycleDuration) > 0
            
            var canTransactionMissVerification = isGenesisBlock || 
                isDistributeCycleRewardsAndPenaltiesTx || isFinishVrfLotteryTx || isFinishCycleTx;
            
            var verifyError = VerifyInternal(receipt, canTransactionMissVerification, HardforkHeights.IsHardfork_9Active(block.Header.Index));
            if (verifyError != OperatingError.Ok)
                return verifyError;
            /* maybe we don't need this check, but I'm afraid */
            if (!receipt.Transaction.FullHash(receipt.Signature,  HardforkHeights.IsHardfork_9Active(block.Header.Index)).Equals(receipt.Hash))
                return OperatingError.HashMismatched;
            /* check transaction nonce */
            var nonce = transactionRepository.GetTotalTransactionCount(receipt.Transaction.From);
            if (nonce != receipt.Transaction.Nonce)
                return OperatingError.InvalidNonce;
            /* try to persist transaction */
            var result = _transactionExecuter.Execute(block, receipt, snapshot);
            if (result != OperatingError.Ok)
            {
                OnTransactionFailed?.Invoke(this, receipt);
                return result;
            }

            /* finalize transaction state */
            OnTransactionExecuted?.Invoke(this, receipt);
            return OperatingError.Ok;
        }

        public OperatingError Verify(TransactionReceipt transaction,  bool useNewChainId)
        {
            return VerifyInternal(transaction, false, useNewChainId);
        }

        private OperatingError VerifyInternal(
            TransactionReceipt acceptedTransaction,
            bool canTransactionMissVerification,
            bool useNewChainId
        )
        {
            /* check if the hash matches */
            if (!Equals(acceptedTransaction.Hash, acceptedTransaction.FullHash(useNewChainId)))
                return OperatingError.HashMismatched;
            /* If it's okay to miss verification, the signature is expected to be empty */
            if (canTransactionMissVerification && acceptedTransaction.Signature.IsZero())
                return OperatingError.Ok;

            var result = VerifySignature(acceptedTransaction, useNewChainId,  true);
            if (result != OperatingError.Ok)
                return result;
            var transaction = acceptedTransaction.Transaction;
            if (transaction.GasLimit > GasMetering.DefaultBlockGasLimit ||
                transaction.GasLimit < GasMetering.DefaultTxCost)
                return OperatingError.InvalidGasLimit;

            if (transaction.Value.ToMoney() > Money.MaxValue)
                return OperatingError.ValueOverflow;
            /* verify transaction via persister */
            return _transactionExecuter.Verify(transaction);
        }

        public OperatingError VerifySignature(TransactionReceipt transaction, ECDSAPublicKey publicKey,  bool useNewChainId)
        {
            if (!_processedTransactions.TryGetValue(transaction.Hash, out var status))
                return _transactionVerifier.VerifyTransactionImmediately(transaction, publicKey, useNewChainId)
                    ? OperatingError.Ok
                    : OperatingError.InvalidSignature;

            return status == TransactionStatus.Verified ? OperatingError.Ok : OperatingError.InvalidSignature;
        }

        public OperatingError VerifySignature(TransactionReceipt transaction, bool useNewChainId,  bool cacheEnabled)
        {
            /* First search the cache to see if the transaction is verified, otherwise verify immediately */
            if (!_processedTransactions.TryGetValue(transaction.Hash, out var status))
                return _transactionVerifier.VerifyTransactionImmediately(transaction, useNewChainId, cacheEnabled)
                    ? OperatingError.Ok
                    : OperatingError.InvalidSignature;
            
            return status == TransactionStatus.Verified ? OperatingError.Ok : OperatingError.InvalidSignature;
        }

        public ulong CalcNextTxNonce(UInt160 from)
        {
            return _stateManager.LastApprovedSnapshot.Transactions.GetTotalTransactionCount(from);
        }

        private bool IsDistributeCycleRewardsAndPenaltiesTx(Block block, Transaction tx) 
        {
            var indexInCycle = block.Header.Index % StakingContract.CycleDuration;
            var cycle = block.Header.Index / StakingContract.CycleDuration;
            if (cycle > 0 && indexInCycle == StakingContract.AttendanceDetectionDuration
                && tx.To.Equals(ContractRegisterer.GovernanceContract))
            {
                var expectedTx = BuildSystemContractTx(ContractRegisterer.GovernanceContract, GovernanceInterface.MethodDistributeCycleRewardsAndPenalties, 
                    UInt256Utils.ToUInt256(GovernanceContract.GetCycleByBlockNumber(block.Header.Index - 1)));
                return CheckSystemTxEquality(expectedTx, tx);
            }
            return false;
        }

        private bool IsFinishVrfLotteryTx(Block block, Transaction tx) 
        {
            var indexInCycle = block.Header.Index % StakingContract.CycleDuration;
            var cycle = block.Header.Index / StakingContract.CycleDuration;
            if (indexInCycle == StakingContract.VrfSubmissionPhaseDuration
                && tx.To.Equals(ContractRegisterer.StakingContract))
            {
                var expectedTx = BuildSystemContractTx(ContractRegisterer.StakingContract,
                    StakingInterface.MethodFinishVrfLottery);
                return CheckSystemTxEquality(expectedTx, tx);
            }
            return false;
        }


        private bool IsFinishCycleTx(Block block, Transaction tx) 
        {
            var indexInCycle = block.Header.Index % StakingContract.CycleDuration;
            var cycle = block.Header.Index / StakingContract.CycleDuration;
            if (cycle > 0 && indexInCycle == 0 && tx.To.Equals(ContractRegisterer.GovernanceContract))
            {
                var expectedTx = BuildSystemContractTx(ContractRegisterer.GovernanceContract, GovernanceInterface.MethodFinishCycle, 
                    UInt256Utils.ToUInt256(GovernanceContract.GetCycleByBlockNumber(block.Header.Index - 1)));
                return CheckSystemTxEquality(expectedTx, tx);
            }
            return false;
        }


        private Transaction BuildSystemContractTx(UInt160 contractAddress, string mehodSignature, params dynamic[] values)
        {
            var transactionRepository = _stateManager.CurrentSnapshot.Transactions;
            var from = UInt160Utils.Zero;
            var nonce = transactionRepository.GetTotalTransactionCount(from);
            var abi = ContractEncoder.Encode(mehodSignature, values);
            return new Transaction
            {
                To = contractAddress,
                Value = UInt256Utils.Zero,
                From = UInt160Utils.Zero,
                Nonce = nonce,
                GasPrice = 0,
                GasLimit = 100000000,
                Invocation = ByteString.CopyFrom(abi),
            };
        }

        private bool CheckSystemTxEquality(Transaction expectedTx, Transaction tx)
        {
            if(expectedTx.Equals(tx)) return true;
            Logger.LogWarning($"System Transactions should match with the expected transaction.");
            Logger.LogDebug($"expected tx: {expectedTx.ToString()}");
            Logger.LogDebug($"Got tx: {tx.ToString()}");
            return false;
        }
    }
}
