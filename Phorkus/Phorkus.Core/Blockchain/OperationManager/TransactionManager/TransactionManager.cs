using System;
using System.Collections.Generic;
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
                {TransactionType.Issue, new IssueTransactionPersister(assetRepository, balanceRepository)},
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
        public event EventHandler<SignedTransaction> OnTransactionConfirmed;
        public event EventHandler<SignedTransaction> OnTransactionSigned;

        public Transaction GetByHash(UInt256 transactionHash)
        {
            var tx = _transactionRepository.GetTransactionByHash(transactionHash);
            return tx != null ? new Transaction(tx.Transaction) : null;
        }

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

        public OperatingError Confirm(UInt256 txHash)
        {
            var signed = _transactionRepository.GetTransactionByHash(txHash);
            if (signed is null)
                return OperatingError.TransactionLost;
            var state = _transactionRepository.GetTransactionState(txHash);
            if (state != null && state.Status == TransactionState.Types.TransactionStatus.Confirmed)
                return OperatingError.InvalidState;
            /* try to persist transaction */
            var persister = _transactionPersisters[signed.Transaction.Type];
            if (persister == null)
                return OperatingError.UnsupportedTransaction;
            var result = persister.Execute(signed.Transaction);
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
            OnTransactionConfirmed?.Invoke(this, signed);
            return OperatingError.Ok;
        }

        public SignedTransaction Sign(Transaction transaction, KeyPair keyPair)
        {
            var hash = transaction.ToHash256();
            /* use raw byte arrays to sign transaction hash */
            transaction.Signature = _crypto.Sign(hash.Buffer.ToByteArray(), keyPair.PrivateKey.Buffer.ToByteArray())
                .ToSignature();
            var signed = new SignedTransaction
            {
                Transaction = transaction,
                Hash = transaction.ToHash256()
            };
            OnTransactionSigned?.Invoke(this, signed);
            return signed;
        }

        public OperatingError VerifySignature(SignedTransaction transaction, PublicKey publicKey)
        {
            var tx = transaction.Transaction;
            var sig = tx.Signature;
            tx.Signature = SignatureUtils.Zero;
            var hash = tx.ToHash256();
            tx.Signature = sig;
            try
            {
                var result = _crypto.VerifySignature(hash.Buffer.ToByteArray(),
                    transaction.Transaction.Signature.Buffer.ToByteArray(), publicKey.Buffer.ToByteArray());
                if (!result)
                    return OperatingError.InvalidSignature;
            }
            catch (Exception)
            {
                return OperatingError.InvalidSignature;
            }

            return OperatingError.Ok;
        }

        public OperatingError VerifySignature(SignedTransaction transaction)
        {
            var tx = transaction.Transaction;
            var sig = tx.Signature;
            tx.Signature = SignatureUtils.Zero;
            var hash = tx.ToHash256();
            tx.Signature = sig;
            byte[] rawKey;
            try
            {
                rawKey = _crypto.RecoverSignature(hash.Buffer.ToByteArray(),
                    transaction.Transaction.Signature.Buffer.ToByteArray(),
                    transaction.Transaction.From.Buffer.ToByteArray());
                if (rawKey is null)
                    return OperatingError.InvalidSignature;
                rawKey = _crypto.DecodePublicKey(rawKey, true, out _, out _);
            }
            catch (Exception)
            {
                return OperatingError.InvalidSignature;
            }
            /* TODO: "i don't think that we need recover here again, cuz signature already verified" */
            return VerifySignature(transaction, rawKey.ToPublicKey());
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