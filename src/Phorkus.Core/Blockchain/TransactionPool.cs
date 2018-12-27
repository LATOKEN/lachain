using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Phorkus.Core.Blockchain.OperationManager;
using Phorkus.Proto;
using Phorkus.Storage.RocksDB.Repositories;

namespace Phorkus.Core.Blockchain
{
    public class TransactionPool : ITransactionPool
    {
        public const int PeekLimit = 1000;

        private readonly ITransactionVerifier _transactionVerifier;
        private readonly ITransactionRepository _transactionRepository;
        
        private readonly ConcurrentDictionary<UInt256, SignedTransaction> _transactions
            = new ConcurrentDictionary<UInt256, SignedTransaction>();
        
        private readonly ConcurrentQueue<SignedTransaction> _transactionsQueue
            = new ConcurrentQueue<SignedTransaction>();

        public TransactionPool(
            ITransactionVerifier transactionVerifier,
            ITransactionRepository transactionRepository)
        {
            _transactionVerifier = transactionVerifier ?? throw new ArgumentNullException(nameof(transactionVerifier));
            _transactionRepository = transactionRepository ?? throw new ArgumentNullException(nameof(transactionRepository));
        }

        public IReadOnlyDictionary<UInt256, SignedTransaction> Transactions => _transactions;
        
        [MethodImpl(MethodImplOptions.Synchronized)]
        public bool Add(SignedTransaction transaction)
        {
            var result = _transactionVerifier.Verify(transaction.Transaction);
            if (result != OperatingError.Ok)
                return false;
            _transactions[transaction.Hash] = transaction;
            _transactionsQueue.Enqueue(transaction);
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
                if (!_transactionsQueue.TryDequeue(out var transaction))
                    continue;
                result.Add(transaction);
            }
            return result;
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