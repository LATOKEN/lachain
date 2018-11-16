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
        
        private readonly ConcurrentDictionary<UInt256, SignedTransaction> _transactions
            = new ConcurrentDictionary<UInt256, SignedTransaction>();
        
        public IReadOnlyDictionary<UInt256, SignedTransaction> Transactions => _transactions;

        public void Add(SignedTransaction transaction)
        {
            _transactions[transaction.Hash] = transaction;
        }

        public IReadOnlyCollection<SignedTransaction> Peek()
        {
            var result = new List<SignedTransaction>();
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