using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Phorkus.Core.Blockchain.OperationManager;
using Phorkus.Core.Utils;
using Phorkus.Networking;
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
        private readonly INetworkManager _networkManager;
        private readonly INetworkBroadcaster _networkBroadcaster;

        private readonly ConcurrentDictionary<UInt256, TransactionReceipt> _transactions
            = new ConcurrentDictionary<UInt256, TransactionReceipt>();

        private readonly ConcurrentQueue<TransactionReceipt> _transactionsQueue =
            new ConcurrentQueue<TransactionReceipt>();

        private readonly ConcurrentQueue<TransactionReceipt> _relayQueue = new ConcurrentQueue<TransactionReceipt>();

        public TransactionPool(
            ITransactionVerifier transactionVerifier,
            IPoolRepository poolRepository,
            ITransactionManager transactionManager,
            INetworkManager networkManager,
            INetworkBroadcaster networkBroadcaster)
        {
            _transactionVerifier = transactionVerifier ?? throw new ArgumentNullException(nameof(transactionVerifier));
            _poolRepository = poolRepository ?? throw new ArgumentNullException(nameof(poolRepository));
            _transactionManager = transactionManager;
            _networkBroadcaster = networkBroadcaster;
            _networkManager = networkManager;

            Restore();
        }

        public IReadOnlyDictionary<UInt256, TransactionReceipt> Transactions => _transactions;

        public TransactionReceipt? GetByHash(UInt256 hash)
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
        public OperatingError Add(Transaction transaction, Signature signature)
        {
            var acceptedTx = new TransactionReceipt
            {
                Transaction = transaction,
                Hash = transaction.ToHash256(),
                Signature = signature,
                Status = TransactionStatus.Pool
            };
            return Add(acceptedTx);
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public OperatingError Add(TransactionReceipt transaction)
        {
            if (transaction is null)
                throw new ArgumentNullException(nameof(transaction));
            /* don't add to transaction pool transactions with the same hashes */
            if (_transactions.ContainsKey(transaction.Hash))
                return OperatingError.AlreadyExists;
            /* verify transaction before adding */
            var result = _transactionManager.Verify(transaction);
            if (result != OperatingError.Ok)
                return result;
            _transactionVerifier.VerifyTransaction(transaction);
            /* put transaction to pool queue */
            _transactions[transaction.Hash] = transaction;
            _transactionsQueue.Enqueue(transaction);
            /* write transaction to persistence storage */
            if (!_poolRepository.ContainsTransactionByHash(transaction.Hash))
                _poolRepository.AddTransaction(transaction);
            if (!_networkManager.IsReady)
                return OperatingError.Ok;
            var message = _networkManager.MessageFactory?.GetTransactionsByHashesReply(
                              new[] {transaction}
                          ) ?? throw new InvalidOperationException();
            _networkBroadcaster.Broadcast(message);
            return OperatingError.Ok;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public IReadOnlyCollection<TransactionReceipt> Peek(int limit = -1)
        {
            if (limit < 0)
                limit = PeekLimit;
            var result = new List<TransactionReceipt>();
            var txToPeek = Math.Min(_transactionsQueue.Count + _relayQueue.Count, limit);
            for (var i = 0; i < txToPeek; i++)
            {
                /* replayed transactions has higher precedence */
                if (!_relayQueue.TryDequeue(out var receipt) && !_transactionsQueue.TryDequeue(out receipt))
                    continue;
                /* remove transaction hash */
                if (!_transactions.TryRemove(receipt.Hash, out _))
                    continue;
                result.Add(receipt);
            }

            return result.OrderByDescending(tx => tx.Transaction.GasPrice)
                .Where(tx => _transactionManager.GetByHash(tx.Hash) == null).ToList();
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Relay(IEnumerable<TransactionReceipt> receipts)
        {
            foreach (var receipt in receipts)
            {
                if (!_transactions.TryAdd(receipt.Hash, receipt))
                    continue;
                _relayQueue.Enqueue(receipt);
            }
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