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
            IContractRepository contractRepository, ICrypto crypto)
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
            _transactionRepository = transactionRepository;
            _crypto = crypto;
        }
        
        public event EventHandler<SignedTransaction> OnTransactionPersisted;
        public event EventHandler<SignedTransaction> OnTransactionSigned;

        public HashedTransaction GetByHash(UInt256 transactionHash)
        {
            var tx = _transactionRepository.GetTransactionByHash(transactionHash);
            return tx != null ? new HashedTransaction(tx) : null;
        }
        
        public void Persist(SignedTransaction transaction)
        {
            var hashed = new HashedTransaction(transaction.Transaction);
            if (Verify(hashed) != OperatingError.Ok)
                throw new InvalidTransactionException();
            var persister = _transactionPersisters[transaction.Transaction.Type];
            if (persister == null)
                throw new InvalidTransactionTypeException();
            _transactionRepository.AddTransaction(transaction.Transaction);
            /* TODO: "prepare persistence here" */
            if (!persister.Persist(transaction.Transaction, transaction.Hash))
                return;
            /* TODO: "finalize persistence here" */
            OnTransactionPersisted?.Invoke(this, transaction);
        }
        
        public SignedTransaction Sign(HashedTransaction transaction, KeyPair keyPair)
        {
            if (Verify(transaction) != OperatingError.Ok)
                throw new InvalidTransactionException();
            var signature = _crypto.Sign(transaction.Hash.Buffer.ToByteArray(), keyPair.PrivateKey).ToSignature();
            var signed = new SignedTransaction
            {
                Transaction = transaction.Transaction,
                Hash = transaction.Hash,
                Signature = signature
            };
            OnTransactionSigned?.Invoke(this, signed);
            return signed;
        }

        public OperatingError VerifySignature(SignedTransaction transaction, PublicKey publicKey)
        {
            var result = _crypto.VerifySignature(transaction.Hash.Buffer.ToByteArray(), transaction.Signature.Buffer.ToByteArray(), publicKey.Buffer.ToByteArray());
            return result ? OperatingError.Ok : OperatingError.InvalidSignature;
        }

        public OperatingError Verify(HashedTransaction transaction)
        {
            var tx = transaction.Transaction;
            /* maybe we don't need this check, but I'm afraid */
            if (!tx.ToHash256().Equals(transaction.Hash))
                return OperatingError.HashMismatched;
            /* validate default transaction attributes */
            if (tx.Version != 0)
                return OperatingError.UnsupportedVersion;
            if (tx.From == null || !tx.From.IsValid())
                return OperatingError.SizeMismatched;
            var lastTx = _transactionRepository.GetLatestTransaction();
            if (lastTx != null && tx.Nonce != lastTx.Nonce + 1)
                return OperatingError.InvalidNonce;
            /* verify transaction via persister */
            var persister = _transactionPersisters[tx.Type];
            if (persister == null)
                return OperatingError.UnsupportedTransaction;
            return persister.Verify(tx);
        }
    }
}