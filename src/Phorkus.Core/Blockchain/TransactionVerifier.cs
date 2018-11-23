using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Phorkus.Core.Blockchain.OperationManager;
using Phorkus.Core.Logging;
using Phorkus.Core.Utils;
using Phorkus.Proto;

namespace Phorkus.Core.Blockchain
{
    public class TransactionVerifier : ITransactionVerifier
    {
        private readonly ITransactionManager _transactionManager;
        private readonly ILogger<ITransactionVerifier> _logger;

        private readonly Queue<SignedTransaction> _transactionQueue
            = new Queue<SignedTransaction>();

        private readonly object _queueNotEmpty = new object();

        public TransactionVerifier(
            ITransactionManager transactionManager,
            ILogger<ITransactionVerifier> logger)
        {
            _transactionManager = transactionManager;
            _logger = logger;
        }

        public event EventHandler<SignedTransaction> OnTransactionVerified;

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

        public bool VerifyTransactionImmediately(SignedTransaction signedTransaction)
        {
            if (signedTransaction is null)
                throw new ArgumentNullException(nameof(signedTransaction));
            if (!signedTransaction.Hash.Equals(signedTransaction.Transaction.ToHash256()))
                return false;
            if (_transactionManager.Verify(signedTransaction.Transaction) != OperatingError.Ok)
                return false;
            return _transactionManager.VerifySignature(signedTransaction) == OperatingError.Ok;
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
            uint workers = 1;
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