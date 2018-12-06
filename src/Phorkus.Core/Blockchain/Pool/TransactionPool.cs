using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Phorkus.Core.Blockchain.OperationManager;
using Phorkus.Proto;

namespace Phorkus.Core.Blockchain.Pool
{
    public class TransactionPool : ITransactionPool
    {
        public const int PeekLimit = 1000;

        private readonly ITransactionVerifier _transactionVerifier;
        private readonly ITransactionManager _transactionManager;
        
        /*private readonly ConcurrentDictionary<UInt256, SignedTransaction> _transactions
            = new ConcurrentDictionary<UInt256, SignedTransaction>();*/
        
        private readonly ConcurrentQueue<SignedTransaction> _transactions
            = new ConcurrentQueue<SignedTransaction>();

        public TransactionPool(
            ITransactionVerifier transactionVerifier,
            ITransactionManager transactionManager)
        {
            _transactionVerifier = transactionVerifier ?? throw new ArgumentNullException(nameof(transactionVerifier));
            _transactionManager = transactionManager ?? throw new ArgumentNullException(nameof(transactionManager));
        }

        public IReadOnlyDictionary<UInt256, SignedTransaction> Transactions => _transactions.ToDictionary(tx => tx.Hash);
        
        public bool Add(SignedTransaction transaction)
        {
            /*var result = _transactionManager.Verify(transaction.Transaction);
            if (result != OperatingError.Ok)
                return false;
            _transactions[transaction.Hash] = transaction;*/
            _transactions.Enqueue(transaction);
            return true;
        }

        public IReadOnlyCollection<SignedTransaction> Peek(int limit = -1)
        {
            if (limit < 0)
                limit = PeekLimit;
            var result = new List<SignedTransaction>();
            var txToPeek = Math.Min(_transactions.Count, limit);
            for (var i = 0; i < txToPeek; i++)
            {
                if (!_transactions.TryDequeue(out var transaction))
                    continue;
                result.Add(transaction);
            }
            return result;
        }

        public uint Size()
        {
            return (uint) _transactions.Count;
        }

        public void Delete(UInt256 transactionHash)
        {
            /*_transactions.TryRemove(transactionHash, out _);*/
        }

        public void Clear()
        {
            /*_transactions.Clear();*/
        }
    }
}