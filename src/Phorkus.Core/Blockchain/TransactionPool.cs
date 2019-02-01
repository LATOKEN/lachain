using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Phorkus.Core.Blockchain.OperationManager;
using Phorkus.Core.Utils;
using Phorkus.Proto;
using Phorkus.Storage.Repositories;
using Phorkus.Utility;

namespace Phorkus.Core.Blockchain
{
    public class TransactionPool : ITransactionPool
    {
        public const int PeekLimit = 1000;

        private readonly ITransactionVerifier _transactionVerifier;
        private readonly IPoolRepository _poolRepository;
        private readonly ITransactionManager _transactionManager;
        
        private readonly ConcurrentDictionary<UInt256, AcceptedTransaction> _transactions
            = new ConcurrentDictionary<UInt256, AcceptedTransaction>();
        
        private readonly ConcurrentQueue<AcceptedTransaction> _transactionsQueue
            = new ConcurrentQueue<AcceptedTransaction>();

        public TransactionPool(
            ITransactionVerifier transactionVerifier,
            IPoolRepository poolRepository,
            ITransactionManager transactionManager)
        {
            _transactionVerifier = transactionVerifier ?? throw new ArgumentNullException(nameof(transactionVerifier));
            _poolRepository = poolRepository ?? throw new ArgumentNullException(nameof(poolRepository));
            _transactionManager = transactionManager;

            Restore();
        }

        public IReadOnlyDictionary<UInt256, AcceptedTransaction> Transactions => _transactions;

        public AcceptedTransaction GetByHash(UInt256 hash)
        {
            return _transactions.TryGetValue(hash, out var tx) ? tx : _poolRepository.GetTransactionByHash(hash);
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Restore()
        {
            var txHashes = _poolRepository.GetTransactionPool();
            foreach (var txHash in txHashes)
            {
                var tx = _poolRepository.GetTransactionByHash(txHash);
                if (tx is null)
                    continue;
                Add(tx);
            }
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public bool Add(Transaction transaction, Signature signature)
        {
            var acceptedTx = new AcceptedTransaction
            {
                Transaction = transaction,
                Hash = transaction.ToHash256(),
                Signature = signature,
                Status = TransactionStatus.Pool
            };
            return Add(acceptedTx);
        }
        
        [MethodImpl(MethodImplOptions.Synchronized)]
        public bool Add(AcceptedTransaction transaction)
        {
            if (transaction is null)
                throw new ArgumentNullException(nameof(transaction));
            /* don't add to transaction pool transactions with the same hashes */
            if (_transactions.ContainsKey(transaction.Hash))
                return false;
            /* verify transaction before adding */
            var result = _transactionManager.Verify(transaction);
            if (result != OperatingError.Ok)
                return false;
            _transactionVerifier.VerifyTransaction(transaction);
            /* put transaction to pool queue */
            _transactions[transaction.Hash] = transaction;
            _transactionsQueue.Enqueue(transaction);
            /* write transaction to persistence storage */
            if (!_poolRepository.ContainsTransactionByHash(transaction.Hash))
                _poolRepository.AddTransaction(transaction);
            return true;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public IReadOnlyCollection<AcceptedTransaction> Peek(int limit = -1)
        {
            if (limit < 0)
                limit = PeekLimit;
            var result = new List<AcceptedTransaction>();
            var txToPeek = Math.Min(_transactionsQueue.Count, limit);
            for (var i = 0; i < txToPeek; i++)
            {
                if (!_transactionsQueue.TryDequeue(out var transaction) || !_transactions.TryRemove(transaction.Hash, out _))
                    continue;
                result.Add(transaction);
            }
            return result.OrderByDescending(tx => tx.Transaction.Fee, new UInt256Comparer()).Where(tx => _transactionManager.GetByHash(tx.Hash) == null).ToList();
        }
        
        [MethodImpl(MethodImplOptions.Synchronized)]
        public uint Size()
        {
            return (uint) _transactions.Count;
        }
        
        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Delete(UInt256 transactionHash)
        {
            _transactions.TryRemove(transactionHash, out _);
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Clear()
        {
            _transactions.Clear();
        }
    }
}