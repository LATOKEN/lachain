using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Phorkus.Core.Proto;

namespace Phorkus.Core.Blockchain.Pool
{
    public class TransactionPool : ITransactionPool
    {
        public const uint PeekLimit = 1000;
        
        private readonly ConcurrentDictionary<UInt256, HashedTransaction> _transactions
            = new ConcurrentDictionary<UInt256, HashedTransaction>();
        
        public IReadOnlyDictionary<UInt256, HashedTransaction> Transactions => _transactions;

        public void Add(HashedTransaction transaction)
        {
            _transactions[transaction.Hash] = transaction;
        }

        public IReadOnlyCollection<HashedTransaction> Peek()
        {
            var result = new List<HashedTransaction>();
            var keys = _transactions.Keys.ToArray();
            for (var i = 0; i < Math.Min(keys.Length, PeekLimit); i++)
            {
                if (!_transactions.TryRemove(keys[i], out var transaction))
                    continue;
                result.Add(transaction);
            }
            return result;
        }

        public void Delete(UInt256 transactionHash)
        {
            _transactions.TryRemove(transactionHash, out _);
        }

        public void Clear()
        {
            _transactions.Clear();
        }
    }
}