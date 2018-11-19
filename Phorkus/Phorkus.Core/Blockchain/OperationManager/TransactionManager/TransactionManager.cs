using System;
using System.Collections.Generic;
using Google.Protobuf;
using Phorkus.Core.Cryptography;
using Phorkus.Core.Proto;
using Phorkus.Core.Storage;
using Phorkus.Core.Utils;

namespace Phorkus.Core.Blockchain.OperationManager.TransactionManager
{
    public class TransactionManager : ITransactionManager
    {
        private readonly ITransactionRepository _transactionRepository;
        private readonly ICrypto _crypto;
        private readonly IReadOnlyDictionary<TransactionType, ITransactionPersister> _transactionPersisters;

        public TransactionManager(
            ITransactionRepository transactionRepository,
            IAssetRepository assetRepository,
            IBalanceRepository balanceRepository,
            IContractRepository contractRepository,
            ICrypto crypto)
        {
            _transactionPersisters = new Dictionary<TransactionType, ITransactionPersister>
            {
                {TransactionType.Miner, new MinerTranscationPersister()},
                {TransactionType.Register, new RegisterTransactionPersister(assetRepository)},
                {TransactionType.Issue, new IssueTransactionPersister(assetRepository)},
                {TransactionType.Contract, new ContractTransactionPersister(balanceRepository)},
                {TransactionType.Publish, new PublishTransactionPersister(contractRepository)},
                {TransactionType.Deposit, new DepositTransactionPersister()},
                {TransactionType.Withdraw, new WithdrawTransactionPersister()}
            };
            _transactionRepository =
                transactionRepository ?? throw new ArgumentNullException(nameof(transactionRepository));
            _crypto = crypto ?? throw new ArgumentNullException(nameof(crypto));
        }

        public event EventHandler<SignedTransaction> OnTransactionPersisted;
        public event EventHandler<SignedTransaction> OnTransactionFailed;
        public event EventHandler<SignedTransaction> OnTransactionSigned;

        public Transaction GetByHash(UInt256 transactionHash)
        {
            var tx = _transactionRepository.GetTransactionByHash(transactionHash);
            return tx != null ? new Transaction(tx.Transaction) : null;
        }

        public OperatingError Persist(SignedTransaction transaction)
        {
            /* verify transaction */
            var verifyError = Verify(transaction.Transaction);
            if (verifyError != OperatingError.Ok)
                return verifyError;
            var persister = _transactionPersisters[transaction.Transaction.Type];
            if (persister == null)
                return OperatingError.UnsupportedTransaction;
            /* maybe we don't need this check, but I'm afraid */
            if (!transaction.Transaction.ToHash256().Equals(transaction.Hash))
                return OperatingError.HashMismatched;
            /* change transaction state to taken */
            _transactionRepository.AddTransaction(transaction);
            var result = persister.Persist(transaction.Transaction, transaction.Hash);
            if (result != OperatingError.Ok)
            {
                _transactionRepository.ChangeTransactionState(transaction.Hash,
                    new TransactionState {Status = TransactionState.Types.TransactionStatus.Failed});
                OnTransactionFailed?.Invoke(this, transaction);
                return result;
            }

            /* finalize transaction state */
            _transactionRepository.ChangeTransactionState(transaction.Hash,
                new TransactionState {Status = TransactionState.Types.TransactionStatus.Confirmed});
            OnTransactionPersisted?.Invoke(this, transaction);
            return OperatingError.Ok;
        }

        public SignedTransaction Sign(Transaction transaction, KeyPair keyPair)
        {
            var hash = transaction.ToHash256();
            var result = Verify(transaction);
            if (result != OperatingError.Ok)
                throw new InvalidTransactionException(result);
            /* use raw byte arrays to sign transaction hash */
            var signature = _crypto.Sign(hash.Buffer.ToByteArray(), keyPair.PrivateKey).ToSignature();
            var signed = new SignedTransaction
            {
                Transaction = transaction,
                Hash = hash,
                Signature = signature
            };
            OnTransactionSigned?.Invoke(this, signed);
            return signed;
        }

        public OperatingError VerifySignature(SignedTransaction transaction, PublicKey publicKey)
        {
            var result = _crypto.VerifySignature(transaction.Hash.Buffer.ToByteArray(),
                transaction.Signature.Buffer.ToByteArray(), publicKey.Buffer.ToByteArray());
            return result ? OperatingError.Ok : OperatingError.InvalidSignature;
        }

        public OperatingError VerifySignature(SignedTransaction transaction)
        {
            var hash = transaction.Transaction.ToHash256();
            if (!hash.Equals(transaction.Hash))
                return OperatingError.HashMismatched;
            var rawKey = _crypto.RecoverSignature(hash.Buffer.ToByteArray(), transaction.Signature.Buffer.ToByteArray(),
                true);
            return rawKey is null
                ? OperatingError.InvalidSignature
                : VerifySignature(transaction, rawKey.ToPublicKey());
        }

        public OperatingError Verify(Transaction transaction)
        {
            /* validate default transaction attributes */
            if (transaction.Version != 0)
                return OperatingError.UnsupportedVersion;
            var latestTx = _transactionRepository.GetLatestTransactionByFrom(transaction.From);
            if (latestTx != null && transaction.Nonce != latestTx.Transaction.Nonce + 1)
                return OperatingError.InvalidNonce;
            /* verify transaction via persister */
            var persister = _transactionPersisters[transaction.Type];
            if (persister == null)
                return OperatingError.UnsupportedTransaction;
            return persister.Verify(transaction);
        }
    }
}