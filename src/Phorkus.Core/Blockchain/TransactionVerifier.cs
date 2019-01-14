using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Phorkus.Core.Blockchain.OperationManager;
using Phorkus.Core.Blockchain.OperationManager.TransactionManager;
using Phorkus.Core.Utils;
using Phorkus.Crypto;
using Phorkus.Logger;
using Phorkus.Proto;
using Phorkus.Storage.Repositories;
using Phorkus.Utility.Utils;

namespace Phorkus.Core.Blockchain
{
    public class TransactionVerifier : ITransactionVerifier
    {
        private readonly ILogger<ITransactionVerifier> _logger;
        private readonly ICrypto _crypto;

        private readonly IDictionary<UInt160, PublicKey> _publicKeyCache
            = new Dictionary<UInt160, PublicKey>();

        private readonly Queue<SignedTransaction> _transactionQueue
            = new Queue<SignedTransaction>();
        
        private readonly IReadOnlyDictionary<TransactionType, ITransactionExecuter> _transactionPersisters;
        

        private readonly object _queueNotEmpty = new object();

        public TransactionVerifier(
            IContractRepository contractRepository,
            IValidatorManager validatorManager,
            IMultisigVerifier multisigVerifier,
            ILogger<ITransactionVerifier> logger,
            ICrypto crypto)
        {
            _crypto = crypto;
            _logger = logger;
            
        
            _transactionPersisters = new Dictionary<TransactionType, ITransactionExecuter>
            {
                {TransactionType.Miner, new MinerTranscationExecuter()},
                {TransactionType.Register, new RegisterTransactionExecuter(multisigVerifier)},
                {TransactionType.Issue, new IssueTransactionExecuter()},
                {TransactionType.Contract, new ContractTransactionExecuter()},
                {TransactionType.Publish, new PublishTransactionExecuter(contractRepository)},
                {TransactionType.Deposit, new DepositTransactionExecuter(validatorManager)},
                {TransactionType.Confirm, new ConfirmTransactionExecuter(validatorManager)}
            };
        }

        public event EventHandler<SignedTransaction> OnTransactionVerified;

        public void VerifyTransaction(SignedTransaction signedTransaction, PublicKey publicKey)
        {
            var address = _crypto.ComputeAddress(publicKey.Buffer.ToByteArray()).ToUInt160();
            _publicKeyCache.Add(address, publicKey);
            VerifyTransaction(signedTransaction);
        }

        public void VerifyTransaction(SignedTransaction signedTransaction)
        {
            if (signedTransaction is null)
                throw new ArgumentNullException(nameof(signedTransaction));
            lock (_queueNotEmpty)
            {
                _transactionQueue.Enqueue(signedTransaction);
                Monitor.PulseAll(_queueNotEmpty);
            }
        }

        public bool VerifyTransactionImmediately(SignedTransaction transaction, PublicKey publicKey)
        {
            try
            {
                /* verify transaction signature */
                var result = _crypto.VerifySignature(transaction.Hash.Buffer.ToByteArray(),
                    transaction.Signature.Buffer.ToByteArray(), publicKey.Buffer.ToByteArray());
                if (!result)
                    return false;
            }
            catch (Exception)
            {
                return false;
            }

            return true;
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

        public bool VerifyTransactionImmediately(SignedTransaction transaction)
        {
            if (transaction is null)
                throw new ArgumentNullException(nameof(transaction));

            /* validate transaction hash */
            if (!transaction.Hash.Equals(transaction.Transaction.ToHash256()))
                return false;

            try
            {
                /* try to verify signature using public key cache to avoid EC recover */
                if (_publicKeyCache.TryGetValue(transaction.Transaction.From, out var publicKey))
                    return VerifyTransactionImmediately(transaction, publicKey);

                /* recover EC to get public key from signature to compute address */
                var rawKey = _crypto.RecoverSignature(transaction.Hash.Buffer.ToByteArray(),
                    transaction.Signature.Buffer.ToByteArray());
                var address = _crypto.ComputeAddress(rawKey);

                /* check if recovered addres from public key is valid */
                if (rawKey is null || !address.SequenceEqual(transaction.Transaction.From.Buffer.ToByteArray()))
                    return false;

                /* try to remember public key for this address */
                _publicKeyCache.Add(transaction.Transaction.From, rawKey.ToPublicKey());
            }
            catch (Exception)
            {
                return false;
            }

            return true;
        }

        private void _Worker()
        {
            while (_working)
            {
                try
                {
                    SignedTransaction tx;
                    lock (_queueNotEmpty)
                    {
                        while (_transactionQueue.Count == 0)
                            Monitor.Wait(_queueNotEmpty);
                        tx = _transactionQueue.Dequeue();
                    }

                    if (VerifyTransactionImmediately(tx))
                        OnTransactionVerified?.Invoke(this, tx);
                }
                catch (Exception e)
                {
                    _logger.LogError("Transaction verified failed: " + e);
                }
            }
        }

        private bool _working;

        public void Start()
        {
            uint workers = 4;
            if (workers <= 0)
                throw new ArgumentOutOfRangeException(nameof(workers));
            if (_working)
                return;
            _working = true;
            for (var i = 0; i < workers; i++)
                Task.Factory.StartNew(_Worker, TaskCreationOptions.LongRunning);
        }

        public void Stop()
        {
            _working = false;
        }
    }
}