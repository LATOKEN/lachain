using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Phorkus.Core.Blockchain.Interface;
using Phorkus.Core.Blockchain.OperationManager;
using Phorkus.Core.Blockchain.Utils;
using Phorkus.Crypto;
using Phorkus.Networking;
using Phorkus.Proto;
using Phorkus.Storage.Repositories;
using Phorkus.Utility.Utils;

namespace Phorkus.Core.Blockchain.Pool
{
    public class TransactionPool : ITransactionPool
    {
        private readonly ITransactionVerifier _transactionVerifier;
        private readonly IPoolRepository _poolRepository;
        private readonly ITransactionManager _transactionManager;
        private readonly INetworkManager _networkManager;
        private readonly INetworkBroadcaster _networkBroadcaster;

        private readonly ConcurrentDictionary<UInt256, TransactionReceipt> _transactions
            = new ConcurrentDictionary<UInt256, TransactionReceipt>();

        private ISet<TransactionReceipt> _transactionsQueue = new HashSet<TransactionReceipt>();
        private ISet<TransactionReceipt> _relayQueue = new HashSet<TransactionReceipt>();

        public TransactionPool(
            ITransactionVerifier transactionVerifier,
            IPoolRepository poolRepository,
            ITransactionManager transactionManager,
            INetworkManager networkManager,
            INetworkBroadcaster networkBroadcaster,
            IBlockManager blockManager
        )
        {
            _transactionVerifier = transactionVerifier ?? throw new ArgumentNullException(nameof(transactionVerifier));
            _poolRepository = poolRepository ?? throw new ArgumentNullException(nameof(poolRepository));
            _transactionManager = transactionManager;
            _networkBroadcaster = networkBroadcaster;
            _networkManager = networkManager;

            blockManager.OnBlockPersisted += BlockManagerOnOnBlockPersisted;
            Restore();
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        private void BlockManagerOnOnBlockPersisted(object sender, Block e)
        {
            foreach (var tx in e.TransactionHashes)
            {
                Delete(tx);
            }
        }

        public IReadOnlyDictionary<UInt256, TransactionReceipt> Transactions => _transactions;

        [MethodImpl(MethodImplOptions.Synchronized)]
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
            Console.WriteLine("Hash");
            Console.WriteLine(HashUtils.ToHash256(transaction).ToHex());
            var acceptedTx = new TransactionReceipt
            {
                Transaction = transaction,
                Hash = HashUtils.ToHash256(transaction),
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
            _transactionsQueue.Add(transaction);
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

        private bool TxNonceValid(TransactionReceipt receipt)
        {
            return receipt.Transaction.Nonce >= _transactionManager.CalcNextTxNonce(receipt.Transaction.From);
        }

        private void Sanitize()
        {
            _relayQueue = new HashSet<TransactionReceipt>(_relayQueue.Where(TxNonceValid));
            _transactionsQueue = new HashSet<TransactionReceipt>(_transactionsQueue.Where(TxNonceValid));
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public IReadOnlyCollection<TransactionReceipt> Peek(int txsToLook, int txsToTake)
        {
            Sanitize();
            var rnd = new Random();
            var result = _relayQueue.ToList();
            // First we try to take transactions from relay queue
            if (result.Count >= txsToTake) return result.Take(txsToTake).ToList();
            txsToTake -= result.Count;

            // We first greedily take some most profitable transactions. Let's group by sender and
            // peek the best by gas price (so we do not break nonce order)
            var txsBySender = _transactionsQueue
                .OrderBy(x => x, new ReceiptComparer())
                .GroupBy(receipt => receipt.Transaction.From)
                .ToDictionary(receipts => receipts.Key, receipts => receipts.Reverse().ToList());

            // We maintain heap of current transaction for each sender
            var heap = new C5.IntervalHeap<TransactionReceipt>(new GasPriceReceiptComparer());
            foreach (var txs in txsBySender.Values)
            {
                heap.Add(txs.Last());
            }

            var bestTxs = new List<TransactionReceipt>();
            for (var i = 0; i < txsToLook && !heap.IsEmpty; ++i)
            {
                var tx = heap.DeleteMin(); // peek best available tx
                bestTxs.Add(tx);
                var txsFrom = txsBySender[tx.Transaction.From];
                txsFrom.RemoveAt(txsFrom.Count - 1);
                if (txsFrom.Count != 0)
                {
                    // If there are more txs from this sender, add them to heap 
                    heap.Add(txsFrom.Last());
                }
            }

            // Regroup transactions in order to take some random subset
            txsBySender = bestTxs
                .OrderBy(x => x, new ReceiptComparer())
                .GroupBy(receipt => receipt.Transaction.From)
                .ToDictionary(receipts => receipts.Key, receipts => receipts.Reverse().ToList());

            for (var i = 0; i < txsToTake && txsBySender.Count > 0; ++i)
            {
                var key = rnd.SelectRandom(txsBySender.Keys);
                var txsFrom = txsBySender[key];
                var tx = txsFrom.Last();
                result.Add(tx);
                txsFrom.RemoveAt(txsFrom.Count - 1);
                if (txsFrom.Count == 0) txsBySender.Remove(key);
            }

            result = result
                .Where(tx => _transactions.TryRemove(tx.Hash, out _))
                .Where(tx => _transactionManager.GetByHash(tx.Hash) is null)
                .ToList();
            foreach (var tx in result)
            {
                _transactionsQueue.Remove(tx);
                _relayQueue.Remove(tx);
            }

            return result;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Relay(IEnumerable<TransactionReceipt> receipts)
        {
            foreach (var receipt in receipts)
            {
                if (!_transactions.TryAdd(receipt.Hash, receipt))
                    continue;
                _relayQueue.Add(receipt);
            }
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public uint Size()
        {
            return (uint) _transactionsQueue.Count;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Delete(UInt256 transactionHash)
        {
            if (_transactions.TryRemove(transactionHash, out var tx))
            {
                _transactionsQueue.Remove(tx);
            }
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Clear()
        {
            _transactions.Clear();
            _transactionsQueue.Clear();
        }
    }
}