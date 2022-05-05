using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Lachain.Core.Blockchain.Interface;
using Lachain.Crypto;
using Lachain.Logger;
using Lachain.Proto;
using Lachain.Utility.Utils;
using NLog.Fluent;

namespace Lachain.Core.Blockchain.Operations
{
    /* 
        Transaction Verification is done asynchronously. We maintain a queue of unverified transactions. 
        When a transaction is added to the pool, this transaction is added to the queue. Verification is only 
        required during the block execution that contains this transaction. So, during the interval of 
        [addition to pool, block execution], it's most likely that this transaction is dequed from the queue
        and verified. Once verified, it invokes an event, that caches this verified transaction. 
        During block execution, to verify a transaction, first it checks the cache, if it can't find it, it
        verifies immediately. 
    */
    public class TransactionVerifier : ITransactionVerifier
    {
        private static readonly ILogger<TransactionVerifier> Logger =
            LoggerFactory.GetLoggerForClass<TransactionVerifier>();

        private readonly ICrypto _crypto = CryptoProvider.GetCrypto();

        private readonly IDictionary<UInt160, ECDSAPublicKey> _publicKeyCache
            = new Dictionary<UInt160, ECDSAPublicKey>();

        /* Queue to store unverified transactions */
        private readonly Queue<KeyValuePair<TransactionReceipt, bool> > _transactionQueue
            = new Queue<KeyValuePair<TransactionReceipt,  bool> >();

        private readonly object _queueNotEmpty = new object();

        public event EventHandler<TransactionReceipt>? OnTransactionVerified;

        public void VerifyTransaction(TransactionReceipt acceptedTransaction, ECDSAPublicKey publicKey, bool useNewChainId)
        {
            var address = _crypto.ComputeAddress(publicKey.EncodeCompressed()).ToUInt160();
            _publicKeyCache.Add(address, publicKey);
            VerifyTransaction(acceptedTransaction, useNewChainId);
        }

        /* Async Verification. Simply adds the transaction to the queue */
        public void VerifyTransaction(TransactionReceipt acceptedTransaction,  bool useNewChainId)
        {
            if (acceptedTransaction is null)
                throw new ArgumentNullException(nameof(acceptedTransaction));
            lock (_queueNotEmpty)
            {
                _transactionQueue.Enqueue(new KeyValuePair<TransactionReceipt, bool>(acceptedTransaction, useNewChainId));
                Monitor.PulseAll(_queueNotEmpty);
            }
        }

        /* Sync Verification, verifies the transaction immediately */
        public bool VerifyTransactionImmediately(TransactionReceipt receipt, ECDSAPublicKey publicKey, bool useNewChainId)
        {
            try
            {
                return _crypto.VerifySignatureHashed(
                    receipt.Transaction.RawHash(useNewChainId).ToBytes(), 
                    receipt.Signature.Encode(), publicKey.EncodeCompressed(), useNewChainId
                );
            }
            catch (Exception)
            {
                return false;
            }
        }

        public bool VerifyTransactionImmediately(TransactionReceipt receipt,  bool useNewChainId, bool cacheEnabled)
        {
            if (receipt is null)
                throw new ArgumentNullException(nameof(receipt));

            /* validate transaction hash */
            if (!receipt.Hash.Equals(receipt.FullHash(useNewChainId)))
                return false;

            try
            {
                /* try to verify signature using public key cache to avoid EC recover */
                if (cacheEnabled && _publicKeyCache.TryGetValue(receipt.Transaction.From, out var publicKey))
                    return VerifyTransactionImmediately(receipt, publicKey, useNewChainId);

                /* recover EC to get public key from signature to compute address */
                publicKey = receipt.RecoverPublicKey(useNewChainId);
                var address = publicKey.GetAddress();

                /* check if recovered address from public key is valid */
                if (!address.Equals(receipt.Transaction.From))
                    return false;

                /* try to remember public key for this address */
                if (cacheEnabled)
                    _publicKeyCache.Add(receipt.Transaction.From, publicKey);
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"Failed to verify transaction: {ex}");
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
                    bool useNewChainId;
                    lock (_queueNotEmpty)
                    {
                        while (_transactionQueue.Count == 0)
                            Monitor.Wait(_queueNotEmpty);
                        var pair =  _transactionQueue.Dequeue();
                        tx = pair.Key;
                        useNewChainId = pair.Value;
                    }

                    if (VerifyTransactionImmediately(tx, useNewChainId,  true))
                        OnTransactionVerified?.Invoke(this, tx);
                }
                catch (Exception e)
                {
                    Logger.LogError("Transaction verification failed: " + e);
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
