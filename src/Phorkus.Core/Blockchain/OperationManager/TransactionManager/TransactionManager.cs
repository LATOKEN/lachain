using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Org.BouncyCastle.Math.EC;
using Phorkus.Proto;
using Phorkus.Core.Utils;
using Phorkus.Core.VM;
using Phorkus.Crypto;
using Phorkus.Storage.Repositories;
using Phorkus.Storage.State;
using Phorkus.Utility.Utils;

namespace Phorkus.Core.Blockchain.OperationManager.TransactionManager
{
    public class TransactionManager : ITransactionManager
    {
        private readonly ITransactionVerifier _transactionVerifier;
        private readonly IReadOnlyDictionary<TransactionType, ITransactionExecuter> _transactionPersisters;
        private readonly ICrypto _crypto;
        private readonly IStateManager _stateManager;

        public TransactionManager(
            IValidatorManager validatorManager,
            ITransactionVerifier transactionVerifier,
            IMultisigVerifier multisigVerifier,
            IVirtualMachine virtualMachine,
            ICrypto crypto,
            IStateManager stateManager)
        {
            _transactionPersisters = new Dictionary<TransactionType, ITransactionExecuter>
            {
                {TransactionType.Miner, new MinerTranscationExecuter()},
                {TransactionType.Register, new RegisterTransactionExecuter(multisigVerifier)},
                {TransactionType.Issue, new IssueTransactionExecuter()},
                {TransactionType.Contract, new ContractTransactionExecuter(virtualMachine)},
                {TransactionType.Deposit, new DepositTransactionExecuter(validatorManager)},
                {TransactionType.Withdraw, new WithdrawTransactionExecuter(validatorManager)},
                {TransactionType.Confirm, new ConfirmTransactionExecuter(validatorManager)},
                {TransactionType.Deploy, new DeployTransactionExecuter(virtualMachine) }
            };
            _stateManager = stateManager ?? throw new ArgumentNullException(nameof(stateManager));
            _crypto = crypto ?? throw new ArgumentNullException(nameof(crypto));
            _transactionVerifier = transactionVerifier ?? throw new ArgumentNullException(nameof(transactionVerifier));

            transactionVerifier.OnTransactionVerified += (sender, transaction) =>
                _verifiedTransactions.TryAdd(transaction.Hash, transaction.Hash);
        }

        private readonly ConcurrentDictionary<UInt256, UInt256> _verifiedTransactions
            = new ConcurrentDictionary<UInt256, UInt256>();

        public event EventHandler<AcceptedTransaction> OnTransactionPersisted;
        public event EventHandler<AcceptedTransaction> OnTransactionFailed;
        public event EventHandler<AcceptedTransaction> OnTransactionExecuted;
        public event EventHandler<AcceptedTransaction> OnTransactionSigned;

        public AcceptedTransaction GetByHash(UInt256 transactionHash)
        {
            return _stateManager.CurrentSnapshot.Transactions.GetTransactionByHash(transactionHash);
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public OperatingError Execute(Block block, AcceptedTransaction transaction, IBlockchainSnapshot snapshot)
        {
            var transactionRepository = _stateManager.CurrentSnapshot.Transactions;
            /* check transaction with this hash in database */
            if (transactionRepository.GetTransactionByHash(transaction.Hash) != null)
                return OperatingError.AlreadyExists;
            /* verify transaction */
            var verifyError = Verify(transaction);
            if (verifyError != OperatingError.Ok)
                return verifyError;
            /* maybe we don't need this check, but I'm afraid */
            if (!transaction.Transaction.ToHash256().Equals(transaction.Hash))
                return OperatingError.HashMismatched;
            /* check is transaction type supported */
            if (!_transactionPersisters.ContainsKey(transaction.Transaction.Type))
                return OperatingError.UnsupportedTransaction;
            var persister = _transactionPersisters[transaction.Transaction.Type];
            if (persister == null)
                return OperatingError.UnsupportedTransaction;
            /* check transaction nonce */
            var nonce = transactionRepository.GetTotalTransactionCount(transaction.Transaction.From);
            if (nonce != transaction.Transaction.Nonce)
                return OperatingError.InvalidNonce;
            /* try to persist transaction */
            var result = persister.Execute(block, transaction.Transaction, snapshot);
            if (result != OperatingError.Ok)
            {
                OnTransactionFailed?.Invoke(this, transaction);
                return result;
            }
            /* finalize transaction state */
            OnTransactionExecuted?.Invoke(this, transaction);
            return OperatingError.Ok;
        }
        
        [MethodImpl(MethodImplOptions.Synchronized)]
        public AcceptedTransaction Sign(Transaction transaction, KeyPair keyPair)
        {
            /* use raw byte arrays to sign transaction hash */
            var message = transaction.ToHash256().Buffer.ToByteArray();
            var signature = _crypto.Sign(message, keyPair.PrivateKey.Buffer.ToByteArray());
            /* we're afraid */
            var pubKey = _crypto.RecoverSignature(message, signature);
            if (!pubKey.SequenceEqual(keyPair.PublicKey.Buffer.ToByteArray()))
                throw new InvalidKeyPairException();
            var signed = new AcceptedTransaction
            {
                Transaction = transaction,
                Hash = transaction.ToHash256(),
                Signature = signature.ToSignature()
            };
            OnTransactionSigned?.Invoke(this, signed);
            return signed;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public OperatingError Verify(AcceptedTransaction acceptedTransaction)
        {
            if (!Equals(acceptedTransaction.Hash, acceptedTransaction.Transaction.ToHash256()))
                return OperatingError.HashMismatched;
            var result = VerifySignature(acceptedTransaction);
            if (result != OperatingError.Ok)
                return result;
            var transaction = acceptedTransaction.Transaction;
            /* validate default transaction attributes */
            if (transaction.Fee is null)
                return OperatingError.InvalidTransaction;
            /* verify transaction via persister */
            var persister = _transactionPersisters[transaction.Type];
            if (persister == null)
                return OperatingError.UnsupportedTransaction;
            return persister.Verify(transaction);
        }
        
        [MethodImpl(MethodImplOptions.Synchronized)]
        public OperatingError VerifySignature(AcceptedTransaction transaction, PublicKey publicKey)
        {
            if (!_verifiedTransactions.ContainsKey(transaction.Hash))
                return _transactionVerifier.VerifyTransactionImmediately(transaction, publicKey)
                    ? OperatingError.Ok
                    : OperatingError.InvalidSignature;
            _verifiedTransactions.TryRemove(transaction.Hash, out _);
            return OperatingError.Ok;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public OperatingError VerifySignature(AcceptedTransaction transaction)
        {
            if (!_verifiedTransactions.ContainsKey(transaction.Hash))
                return _transactionVerifier.VerifyTransactionImmediately(transaction)
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