using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using NeoSharp.Core.Exceptions;
using NeoSharp.Core.Models;
using NeoSharp.Core.Models.OperationManager;
using NeoSharp.Types;

namespace NeoSharp.Core.Blockchain
{
    internal class TimeStampedTransaction
    {
        public Transaction Transaction { get; }
        public DateTime CreatedAt { get; }

        public TimeStampedTransaction(Transaction transaction)
        {
            Transaction = transaction;
            CreatedAt = DateTime.UtcNow;
        }
    }

    internal class TimeStampedTransactionComparer : IComparer<TimeStampedTransaction>
    {
        private readonly IComparer<Transaction> _comparer;
        private readonly Comparer<DateTime> _createdAtComparer = Comparer<DateTime>.Default;

        public TimeStampedTransactionComparer(IComparer<Transaction> comparer = null)
        {
            _comparer = comparer ?? Comparer<Transaction>.Default;
        }

        public int Compare(TimeStampedTransaction a, TimeStampedTransaction b)
        {
            if (a == b) return 0;
            if (a == null) return -1;
            if (b == null) return 1;

            var transactionComparisonResult = _comparer.Compare(a.Transaction, b.Transaction);

            return transactionComparisonResult == 0
                ? _createdAtComparer.Compare(a.CreatedAt, b.CreatedAt)
                : transactionComparisonResult;
        }
    }

    public class TransactionPool : ITransactionPool
    {
        private const int DefaultCapacity = 50_000;

        private readonly ITransactionOperationsManager _transactionOperationsManager;
        private readonly IComparer<TimeStampedTransaction> _comparer;

        private readonly ConcurrentDictionary<UInt256, TimeStampedTransaction> _transactions =
            new ConcurrentDictionary<UInt256, TimeStampedTransaction>();

        public TransactionPool(ITransactionOperationsManager transactionOperationsManager,
            IComparer<Transaction> comparer = null)
        {
            _transactionOperationsManager = transactionOperationsManager;
            _comparer = new TimeStampedTransactionComparer(comparer);
        }

        public int Capacity => DefaultCapacity;
        public int Size => _transactions.Count;

        IEnumerable<Transaction> ITransactionPool.GetTransactions()
        {
            throw new NotImplementedException();
        }

        public bool Contains(UInt256 hash)
        {
            if (hash == null || hash == UInt256.Zero)
                throw new ArgumentNullException(nameof(hash));
            return _transactions.ContainsKey(hash);
        }

        public Transaction FindByHash(UInt256 hash)
        {
            return _transactions[hash].Transaction;
        }

        public void Add(Transaction transaction)
        {
            if (transaction == null)
                throw new ArgumentNullException(nameof(transaction));

            _transactionOperationsManager.Sign(transaction);

            if (!_transactionOperationsManager.Verify(transaction))
            {
                throw new InvalidTransactionException(
                    $"The transaction with hash \"{transaction.Hash}\" was not passed verification.");
            }

            /*if (this.Where(p => p != transaction)
                .SelectMany(p => p.Inputs)
                .Intersect(transaction.Inputs)
                .Any())
            {
                throw new InvalidTransactionException($"The transaction with hash \"{transaction.Hash}\" was already queued to be added.");
            }*/

            if (!_transactions.TryAdd(transaction.Hash, new TimeStampedTransaction(transaction)))
            {
                throw new InvalidTransactionException(
                    $"The transaction with hash \"{transaction.Hash}\" was already queued to be added.");
            }
        }

        public void Remove(UInt256 hash)
        {
            if (hash == null || hash == UInt256.Zero)
                throw new ArgumentException(nameof(hash));
            _transactions.TryRemove(hash, out _);
        }
        
        private IEnumerable<Transaction> GetTransactions()
        {
            return _transactions.Values.OrderBy(_ => _, _comparer).Select(tst => tst.Transaction);
        }
    }
}