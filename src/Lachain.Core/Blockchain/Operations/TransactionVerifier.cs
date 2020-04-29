using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Lachain.Core.Blockchain.Interface;
using Lachain.Crypto;
using Lachain.Logger;
using Lachain.Proto;
using Lachain.Utility.Utils;

namespace Lachain.Core.Blockchain.Operations
{
    public class TransactionVerifier : ITransactionVerifier
    {
        private readonly ILogger<TransactionVerifier> _logger = LoggerFactory.GetLoggerForClass<TransactionVerifier>();
        private readonly ICrypto _crypto = CryptoProvider.GetCrypto();

        private readonly IDictionary<UInt160, ECDSAPublicKey> _publicKeyCache
            = new Dictionary<UInt160, ECDSAPublicKey>();

        private readonly Queue<TransactionReceipt> _transactionQueue
            = new Queue<TransactionReceipt>();

        private readonly object _queueNotEmpty = new object();

        public event EventHandler<TransactionReceipt>? OnTransactionVerified;

        public void VerifyTransaction(TransactionReceipt acceptedTransaction, ECDSAPublicKey publicKey)
        {
            var address = _crypto.ComputeAddress(publicKey.EncodeCompressed()).ToUInt160();
            _publicKeyCache.Add(address, publicKey);
            VerifyTransaction(acceptedTransaction);
        }

        public void VerifyTransaction(TransactionReceipt acceptedTransaction)
        {
            if (acceptedTransaction is null)
                throw new ArgumentNullException(nameof(acceptedTransaction));
            lock (_queueNotEmpty)
            {
                _transactionQueue.Enqueue(acceptedTransaction);
                Monitor.PulseAll(_queueNotEmpty);
            }
        }

        public bool VerifyTransactionImmediately(TransactionReceipt receipt, ECDSAPublicKey publicKey)
        {
            try
            {
                return _crypto.VerifySignatureHashed(
                    receipt.Transaction.RawHash().ToBytes(), receipt.Signature.Encode(), publicKey.EncodeCompressed()
                );
            }
            catch (Exception)
            {
                return false;
            }
        }

        public bool VerifyTransactionImmediately(TransactionReceipt receipt, bool cacheEnabled = true)
        {
            if (receipt is null)
                throw new ArgumentNullException(nameof(receipt));

            /* validate transaction hash */
            if (!receipt.Hash.Equals(receipt.FullHash()))
                return false;

            try
            {
                /* try to verify signature using public key cache to avoid EC recover */
                if (cacheEnabled && _publicKeyCache.TryGetValue(receipt.Transaction.From, out var publicKey))
                    return VerifyTransactionImmediately(receipt, publicKey);

                /* recover EC to get public key from signature to compute address */
                publicKey = receipt.RecoverPublicKey();
                var address = publicKey.GetAddress();

                /* check if recovered address from public key is valid */
                if (!address.Equals(receipt.Transaction.From))
                    return false;

                /* try to remember public key for this address */
                if (cacheEnabled)
                    _publicKeyCache.Add(receipt.Transaction.From, publicKey);
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
                    TransactionReceipt tx;
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
                    _logger.LogError("Transaction verification failed: " + e);
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