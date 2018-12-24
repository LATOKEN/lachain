using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Phorkus.Core.Blockchain.State;
using Phorkus.Proto;
using Phorkus.Core.Storage;
using Phorkus.Core.Utils;
using Phorkus.Crypto;
using Phorkus.Utility.Utils;

namespace Phorkus.Core.Blockchain.OperationManager.TransactionManager
{
    public class TransactionManager : ITransactionManager
    {
        private readonly ITransactionRepository _transactionRepository;
        private readonly ICrypto _crypto;
        private readonly IReadOnlyDictionary<TransactionType, ITransactionExecuter> _transactionPersisters;
        private readonly ITransactionVerifier _transactionVerifier;

        public TransactionManager(
            ITransactionRepository transactionRepository,
            IContractRepository contractRepository,
            IValidatorManager validatorManager,
            ITransactionVerifier transactionVerifier,
            IMultisigVerifier multisigVerifier,
            ICrypto crypto)
        {
            _transactionPersisters = new Dictionary<TransactionType, ITransactionExecuter>
            {
                {TransactionType.Miner, new MinerTranscationExecuter()},
                {TransactionType.Register, new RegisterTransactionExecuter(multisigVerifier)},
                {TransactionType.Issue, new IssueTransactionExecuter()},
                {TransactionType.Contract, new ContractTransactionExecuter()},
                {TransactionType.Publish, new PublishTransactionExecuter(contractRepository)},
                {TransactionType.Deposit, new DepositTransactionExecuter(validatorManager)},
                {TransactionType.Withdraw, new WithdrawTransactionExecuter(validatorManager)},
                {TransactionType.Confirm, new ConfirmTransactionExecuter(validatorManager)}
            };
            _transactionRepository =
                transactionRepository ?? throw new ArgumentNullException(nameof(transactionRepository));
            _crypto = crypto ?? throw new ArgumentNullException(nameof(crypto));
            _transactionVerifier = transactionVerifier ?? throw new ArgumentNullException(nameof(transactionVerifier));

            transactionVerifier.OnTransactionVerified += (sender, transaction) =>
                _verifiedTransactions.TryAdd(transaction.Hash, transaction.Hash);
        }

        private readonly ConcurrentDictionary<UInt256, UInt256> _verifiedTransactions
            = new ConcurrentDictionary<UInt256, UInt256>();

        public event EventHandler<SignedTransaction> OnTransactionPersisted;
        public event EventHandler<SignedTransaction> OnTransactionFailed;
        public event EventHandler<SignedTransaction> OnTransactionExecuted;
        public event EventHandler<SignedTransaction> OnTransactionSigned;

        public SignedTransaction GetByHash(UInt256 transactionHash)
        {
            var tx = _transactionRepository.GetTransactionByHash(transactionHash);
            return tx;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public OperatingError Persist(SignedTransaction transaction)
        {
            /* check transaction with this hash in database */
            if (_transactionRepository.ContainsTransactionByHash(transaction.Hash))
                return OperatingError.Ok;
            /* verify transaction signature */
            var sigVerifyError = VerifySignature(transaction);
            if (sigVerifyError != OperatingError.Ok)
                return sigVerifyError;
            /* verify transaction */
            var verifyError = Verify(transaction.Transaction);
            if (verifyError != OperatingError.Ok)
                return verifyError;
            if (!_transactionPersisters.ContainsKey(transaction.Transaction.Type))
                return OperatingError.UnsupportedTransaction;
            /* maybe we don't need this check, but I'm afraid */
            if (!transaction.Transaction.ToHash256().Equals(transaction.Hash))
                return OperatingError.HashMismatched;
            var persister = _transactionPersisters[transaction.Transaction.Type];
            if (persister == null)
                return OperatingError.UnsupportedTransaction;
            var result = persister.Verify(transaction.Transaction);
            if (result != OperatingError.Ok)
                return result;
            /* change transaction state to taken */
            _transactionRepository.AddTransaction(transaction);
            OnTransactionPersisted?.Invoke(this, transaction);
            return OperatingError.Ok;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public OperatingError Execute(Block block, UInt256 txHash, IBlockchainSnapshot snapshot)
        {
            var signed = _transactionRepository.GetTransactionByHash(txHash);
            if (signed is null)
                return OperatingError.TransactionLost;
            var latestTx = _transactionRepository.GetLatestTransactionByFrom(signed.Transaction.From);
            if (latestTx != null && latestTx.Transaction.Nonce != 0 && signed.Transaction.Nonce != latestTx.Transaction.Nonce + 1)
                return OperatingError.InvalidNonce;
            /*var state = _transactionRepository.GetTransactionState(txHash);
            if (state != null && state.Status == TransactionState.Types.TransactionStatus.Confirmed)
                return OperatingError.InvalidState;*/
            /* try to persist transaction */
            var persister = _transactionPersisters[signed.Transaction.Type];
            if (persister == null)
                return OperatingError.UnsupportedTransaction;
            var result = persister.Execute(block, signed.Transaction, snapshot);
            if (result != OperatingError.Ok)
            {
                _transactionRepository.ChangeTransactionState(txHash,
                    new TransactionState {Status = TransactionState.Types.TransactionStatus.Failed});
                OnTransactionFailed?.Invoke(this, signed);
                return result;
            }
            /* finalize transaction state */
            _transactionRepository.ChangeTransactionState(txHash,
                new TransactionState {Status = TransactionState.Types.TransactionStatus.Confirmed});
            OnTransactionExecuted?.Invoke(this, signed);
            /* commit new transaction nonce */
            _transactionRepository.CommitTransaction(signed);
            return OperatingError.Ok;
        }
        
        public SignedTransaction Sign(Transaction transaction, KeyPair keyPair)
        {
            /* use raw byte arrays to sign transaction hash */
            var message = transaction.ToHash256().Buffer.ToByteArray();
            var signature = _crypto.Sign(message, keyPair.PrivateKey.Buffer.ToByteArray());
            /* we're afraid */
            var pubKey = _crypto.RecoverSignature(message, signature);
            if (!pubKey.SequenceEqual(keyPair.PublicKey.Buffer.ToByteArray()))
                throw new InvalidKeyPairException();
            var signed = new SignedTransaction
            {
                Transaction = transaction,
                Hash = transaction.ToHash256(),
                Signature = signature.ToSignature()
            };
            OnTransactionSigned?.Invoke(this, signed);
            return signed;
        }

        public OperatingError VerifySignature(SignedTransaction transaction, PublicKey publicKey)
        {
            if (!_verifiedTransactions.ContainsKey(transaction.Hash))
                return _transactionVerifier.VerifyTransactionImmediately(transaction, publicKey)
                    ? OperatingError.Ok
                    : OperatingError.InvalidSignature;
            _verifiedTransactions.TryRemove(transaction.Hash, out _);
            return OperatingError.Ok;
        }

        public OperatingError VerifySignature(SignedTransaction transaction)
        {
            if (!_verifiedTransactions.ContainsKey(transaction.Hash))
                return _transactionVerifier.VerifyTransactionImmediately(transaction)
                    ? OperatingError.Ok
                    : OperatingError.InvalidSignature;
            _verifiedTransactions.TryRemove(transaction.Hash, out _);
            return OperatingError.Ok;
        }

        public OperatingError Verify(Transaction transaction)
        {
            /* validate default transaction attributes */
            if (transaction.Version != 0)
                return OperatingError.UnsupportedVersion;
            /* verify transaction via persister */
            var persister = _transactionPersisters[transaction.Type];
            if (persister == null)
                return OperatingError.UnsupportedTransaction;
            return persister.Verify(transaction);
        }

        public uint CalcNextTxNonce(UInt160 from)
        {
            return _transactionRepository.GetTotalTransactionCount(from);
        }
    }
}