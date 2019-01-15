using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Phorkus.Core.Blockchain.OperationManager;
using Phorkus.Proto;
using Phorkus.Storage.Repositories;
using Phorkus.Utility;

namespace Phorkus.Core.Blockchain
{
    public class TransactionPool : ITransactionPool
    {
        public const int PeekLimit = 1000;

        private readonly ITransactionRepository _transactionRepository;
        private readonly ITransactionVerifier _transactionVerifier;
        private readonly ITransactionManager _transactionManager;
        
        private readonly ConcurrentDictionary<UInt256, SignedTransaction> _transactions
            = new ConcurrentDictionary<UInt256, SignedTransaction>();
        
        private readonly ConcurrentQueue<SignedTransaction> _transactionsQueue
            = new ConcurrentQueue<SignedTransaction>();

        public TransactionPool(
            ITransactionVerifier transactionVerifier,
            ITransactionRepository transactionRepository, ITransactionManager transactionManager)
        {
            _transactionVerifier = transactionVerifier ?? throw new ArgumentNullException(nameof(transactionVerifier));
            _transactionRepository = transactionRepository ?? throw new ArgumentNullException(nameof(transactionRepository));
            _transactionManager = transactionManager;

            Restore();
        }

        public IReadOnlyDictionary<UInt256, SignedTransaction> Transactions => _transactions;

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Restore()
        {
            var txHashes = _transactionRepository.GetTransactionPool();
            foreach (var txHash in txHashes)
            {
                var tx = _transactionRepository.GetTransactionByHash(txHash);
                if (tx is null)
                    continue;
                Add(tx);
            }
        }
        
        [MethodImpl(MethodImplOptions.Synchronized)]
        public bool Add(SignedTransaction transaction)
        {
            if (transaction is null)
                throw new ArgumentNullException(nameof(transaction));
            /* don't add to transaction pool transactions with the same hashes */
            if (_transactions.ContainsKey(transaction.Hash))
                return false;
            /* verify transaction before adding */
            var result = _transactionManager.Verify(transaction.Transaction);
            if (result != OperatingError.Ok)
                return false;
            _transactionVerifier.VerifyTransaction(transaction);
            /* put transaction to pool queue */
            _transactions[transaction.Hash] = transaction;
            _transactionsQueue.Enqueue(transaction);
            /* write transaction to persistence storage */
            if (!_transactionRepository.ContainsTransactionByHash(transaction.Hash))
                _transactionRepository.AddTransaction(transaction);
            return true;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public IReadOnlyCollection<SignedTransaction> Peek(int limit = -1)
        {
            if (limit < 0)
                limit = PeekLimit;
            var result = new List<SignedTransaction>();
            var txToPeek = Math.Min(_transactionsQueue.Count, limit);
            for (var i = 0; i < txToPeek; i++)
            {
                if (!_transactionsQueue.TryDequeue(out var transaction) || !_transactions.TryRemove(transaction.Hash, out _))
                    continue;
                result.Add(transaction);
            }
            return result.OrderByDescending(tx => tx.Transaction.Fee, new UInt256Comparer()).ToList();
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