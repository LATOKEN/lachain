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
using Lachain.Logger;

namespace Lachain.Core.Blockchain.Operations
{
    public class TransactionManager : ITransactionManager
    {
        private static readonly ILogger<TransactionManager> Logger = LoggerFactory.GetLoggerForClass<TransactionManager>();

        private readonly ITransactionVerifier _transactionVerifier;
        private readonly IStateManager _stateManager;
        private readonly TransactionExecuter _transactionExecuter;

        private readonly ConcurrentDictionary<UInt256, UInt256> _verifiedTransactions
            = new ConcurrentDictionary<UInt256, UInt256>();

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
            transactionVerifier.OnTransactionVerified += (sender, transaction) =>
                _verifiedTransactions.TryAdd(transaction.Hash, transaction.Hash);
        }

        public TransactionReceipt? GetByHash(UInt256 transactionHash)
        {
            return _stateManager.CurrentSnapshot.Transactions.GetTransactionByHash(transactionHash);
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public OperatingError Execute(Block block, TransactionReceipt receipt, IBlockchainSnapshot snapshot)
        {
            var transactionRepository = _stateManager.CurrentSnapshot.Transactions;
            /* check transaction with this hash in database */

            Logger.LogTrace($"Before getting tx by hash");
            if (transactionRepository.GetTransactionByHash(receipt.Hash) != null)
                return OperatingError.AlreadyExists;
            /* verify transaction */

            Logger.LogTrace($"After getting tx by hash");
            var indexInCycle = block.Header.Index % StakingContract.CycleDuration;
            var cycle = block.Header.Index / StakingContract.CycleDuration;

            var canTransactionMissVerification =
                block.Header.Index == 0 ||
                cycle > 0 && indexInCycle == StakingContract.AttendanceDetectionDuration &&
                receipt.Transaction.To.Equals(ContractRegisterer.GovernanceContract) ||
                indexInCycle == StakingContract.VrfSubmissionPhaseDuration &&
                receipt.Transaction.To.Equals(ContractRegisterer.StakingContract) ||
                cycle > 0 && indexInCycle == 0 && receipt.Transaction.To.Equals(ContractRegisterer.GovernanceContract);

            var verifyError = VerifyInternal(receipt, canTransactionMissVerification);
            if (verifyError != OperatingError.Ok)
                return verifyError;
            /* maybe we don't need this check, but I'm afraid */
            if (!receipt.Transaction.FullHash(receipt.Signature).Equals(receipt.Hash))
                return OperatingError.HashMismatched;
            /* check transaction nonce */

            Logger.LogTrace($"Before getting nonce");
            var nonce = transactionRepository.GetTotalTransactionCount(receipt.Transaction.From);
            Logger.LogTrace($"After getting nonce");
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

        [MethodImpl(MethodImplOptions.Synchronized)]
        public OperatingError Verify(TransactionReceipt transaction)
        {
            return VerifyInternal(transaction, false);
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        private OperatingError VerifyInternal(
            TransactionReceipt acceptedTransaction,
            bool canTransactionMissVerification
        )
        {
            if (!Equals(acceptedTransaction.Hash, acceptedTransaction.FullHash()))
                return OperatingError.HashMismatched;

            if (canTransactionMissVerification && acceptedTransaction.Signature.IsZero())
                return OperatingError.Ok;

            var result = VerifySignature(acceptedTransaction);
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

        [MethodImpl(MethodImplOptions.Synchronized)]
        public OperatingError VerifySignature(TransactionReceipt transaction, ECDSAPublicKey publicKey)
        {
            if (!_verifiedTransactions.ContainsKey(transaction.Hash))
                return _transactionVerifier.VerifyTransactionImmediately(transaction, publicKey)
                    ? OperatingError.Ok
                    : OperatingError.InvalidSignature;
            _verifiedTransactions.TryRemove(transaction.Hash, out _);
            return OperatingError.Ok;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public OperatingError VerifySignature(TransactionReceipt transaction, bool cacheEnabled = true)
        {
            if (!_verifiedTransactions.ContainsKey(transaction.Hash))
                return _transactionVerifier.VerifyTransactionImmediately(transaction, cacheEnabled)
                    ? OperatingError.Ok
                    : OperatingError.InvalidSignature;
            _verifiedTransactions.TryRemove(transaction.Hash, out _);
            return OperatingError.Ok;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public ulong CalcNextTxNonce(UInt160 from)
        {
            return _stateManager.LastApprovedSnapshot.Transactions.GetTotalTransactionCount(from);
        }
    }
}