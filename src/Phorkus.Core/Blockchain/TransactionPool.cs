using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using NBitcoin.RPC;
using Phorkus.Core.Blockchain.OperationManager;
using Phorkus.Proto;
using Phorkus.Storage.Repositories;

namespace Phorkus.Core.Blockchain
{
    public class TransactionPool : ITransactionPool
    {
        public const int PeekLimit = 1000;

        private readonly ITransactionRepository _transactionRepository;
        private readonly ITransactionVerifier _transactionVerifier;
        
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
            var result = _transactionVerifier.Verify(transaction.Transaction);
            if (result != OperatingError.Ok)
                return false;
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
            var result = new Dictionary<UInt256, SignedTransaction>();
            var txToPeek = Math.Min(_transactionsQueue.Count, limit);
            for (var i = 0; i < txToPeek; i++)
            {
                if (_transactionsQueue.TryDequeue(out var transaction) && !result.ContainsKey(transaction.Hash))
                    result.Add(transaction.Hash, transaction);
            }
            return result.Values;
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