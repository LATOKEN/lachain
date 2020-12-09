using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Lachain.Core.Blockchain.Error;
using Lachain.Core.Blockchain.Interface;
using Lachain.Core.Blockchain.SystemContracts.ContractManager;
using Lachain.Crypto;
using Lachain.Logger;
using Lachain.Proto;
using Lachain.Storage.Repositories;
using Lachain.Utility.Utils;

namespace Lachain.Core.Blockchain.Pool
{
    public class TransactionPool : ITransactionPool
    {
        private static readonly ILogger<TransactionPool> Logger = LoggerFactory.GetLoggerForClass<TransactionPool>();

        private readonly ITransactionVerifier _transactionVerifier;
        private readonly IPoolRepository _poolRepository;
        private readonly ITransactionManager _transactionManager;
        private readonly IBlockManager _blockManager;

        private readonly ConcurrentDictionary<UInt256, TransactionReceipt> _transactions
            = new ConcurrentDictionary<UInt256, TransactionReceipt>();

        private readonly ConcurrentDictionary<UInt160, ulong> _lastNonceForAddress =
            new ConcurrentDictionary<UInt160, ulong>();

        private ISet<TransactionReceipt> _transactionsQueue;
        private ISet<TransactionReceipt> _relayQueue;

        public event EventHandler<TransactionReceipt>? TransactionAdded;
        public IReadOnlyDictionary<UInt256, TransactionReceipt> Transactions => _transactions;

        public TransactionPool(
            ITransactionVerifier transactionVerifier,
            IPoolRepository poolRepository,
            ITransactionManager transactionManager,
            IBlockManager blockManager
        )
        {
            _transactionVerifier = transactionVerifier;
            _poolRepository = poolRepository;
            _transactionManager = transactionManager;
            _blockManager = blockManager;
            _transactionsQueue = new HashSet<TransactionReceipt>();
            _relayQueue = new HashSet<TransactionReceipt>();

            _blockManager.OnBlockPersisted += OnBlockPersisted;
            Restore();
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        private void OnBlockPersisted(object sender, Block e)
        {
            Sanitize();
            foreach (var txHash in e.TransactionHashes)
            {
                Delete(txHash);
            }
        }

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
            var acceptedTx = new TransactionReceipt
            {
                Transaction = transaction,
                Hash = transaction.FullHash(signature),
                Signature = signature,
                Status = TransactionStatus.Pool
            };
            return Add(acceptedTx);
        }

        private void UpdateNonceForAddress(UInt160 address, ulong nonce)
        {
            if (!_lastNonceForAddress.TryGetValue(address, out var lastNonce) || nonce > lastNonce)
            {
                _lastNonceForAddress[address] = nonce;
            }
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public OperatingError Add(TransactionReceipt receipt)
        {
            if (receipt is null)
                throw new ArgumentNullException(nameof(receipt));

            /* don't add to transaction pool transactions with the same hashes */
            if (_transactions.ContainsKey(receipt.Hash))
                return OperatingError.AlreadyExists;
            /* verify transaction before adding */
            if (GetNextNonceForAddress(receipt.Transaction.From) != receipt.Transaction.Nonce)
                return OperatingError.InvalidNonce;

            /* special case for system transactions */
            if (receipt.Transaction.From.IsZero())
            {
                if (!_poolRepository.ContainsTransactionByHash(receipt.Hash))
                    _poolRepository.AddTransaction(receipt);
                return OperatingError.Ok;
            }

            var result = _transactionManager.Verify(receipt);
            if (result != OperatingError.Ok)
                return result;
            _transactionVerifier.VerifyTransaction(receipt);
            /* put transaction to pool queue */
            _transactions[receipt.Hash] = receipt;
            _transactionsQueue.Add(receipt);

            /* write transaction to persistence storage */
            if (!_poolRepository.ContainsTransactionByHash(receipt.Hash))
                _poolRepository.AddTransaction(receipt);

            UpdateNonceForAddress(receipt.Transaction.From, receipt.Transaction.Nonce);
            Logger.LogTrace($"Added transaction {receipt.Hash.ToHex()} to pool");
            TransactionAdded?.Invoke(this, receipt);
            return OperatingError.Ok;
        }

        private bool TxNonceValid(TransactionReceipt receipt)
        {
            return receipt.Transaction.Nonce >= _transactionManager.CalcNextTxNonce(receipt.Transaction.From);
        }

        private bool IsGovernanceTx(TransactionReceipt receipt)
        {
            return receipt.Transaction.To == ContractRegisterer.GovernanceContract;
        }

        private void Sanitize()
        {
            var wasRelayQueueSize = _relayQueue.Count;
            var wasTransactionsQueue = _transactionsQueue.Count;
            _relayQueue = new HashSet<TransactionReceipt>(_relayQueue.Where(TxNonceValid));
            _transactionsQueue = new HashSet<TransactionReceipt>(_transactionsQueue.Where(TxNonceValid));
            if (wasRelayQueueSize != _relayQueue.Count || wasTransactionsQueue != _transactionsQueue.Count)
            {
                Logger.LogTrace(
                    $"Sanitized mempool; dropped {wasTransactionsQueue - _transactionsQueue.Count} txs" +
                    $" from queue & {wasRelayQueueSize - _relayQueue.Count} tx from relay queue"
                );
            }
        }

        private void RemoveTxes(IReadOnlyCollection<TransactionReceipt> txes)
        {
            foreach (var tx in txes)
            {
                _transactionsQueue.Remove(tx);
                _relayQueue.Remove(tx);
            }
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public IReadOnlyCollection<TransactionReceipt> Peek(int txsToLook, int txsToTake)
        {
            Sanitize();
            var rnd = new Random();
            // First,  get governance txes from relay queue
            var result = new HashSet<TransactionReceipt>(_relayQueue.Where(IsGovernanceTx));
            if (result.Count >= txsToTake)
            {
                result = new HashSet<TransactionReceipt>(result
                    .Take(txsToTake)
                    .Where(tx => _transactions.TryRemove(tx.Hash, out _))
                    .Where(tx => _transactionManager.GetByHash(tx.Hash) is null));
                RemoveTxes(result);
                return result.ToList();
            }
            // Get governance txes from tx queue
            result.UnionWith(_transactionsQueue.Where(IsGovernanceTx));
            if (result.Count >= txsToTake)
            {
                result = new HashSet<TransactionReceipt>(result
                    .Take(txsToTake)
                    .Where(tx => _transactions.TryRemove(tx.Hash, out _))
                    .Where(tx => _transactionManager.GetByHash(tx.Hash) is null));
                RemoveTxes(result);
                return result.ToList();
            }
            // Remove selected txes (TODO: Check if we can loss this txes due to the exceptions)
            RemoveTxes(result);
            // Take the rest of transactions from relay queue
            result.UnionWith(_relayQueue);
            if (result.Count >= txsToTake)
            {
                result = new HashSet<TransactionReceipt>(result
                    .Take(txsToTake)
                    .Where(tx => _transactions.TryRemove(tx.Hash, out _))
                    .Where(tx => _transactionManager.GetByHash(tx.Hash) is null));
                RemoveTxes(result);
                return result.ToList();
            }
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

            result = new HashSet<TransactionReceipt>(result
                .Where(tx => _transactions.TryRemove(tx.Hash, out _))
                .Where(tx => _transactionManager.GetByHash(tx.Hash) is null));
            RemoveTxes(result);

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

        [MethodImpl(MethodImplOptions.Synchronized)]
        public ulong? GetMaxNonceForAddress(UInt160 address)
        {
            if (!_lastNonceForAddress.TryGetValue(address, out var lastNonce)) return null;
            return lastNonce;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public ulong GetNextNonceForAddress(UInt160 address)
        {
            var poolNonce = GetMaxNonceForAddress(address);
            var stateNonce = _transactionManager.CalcNextTxNonce(address);
            return poolNonce.HasValue ? Math.Max(poolNonce.Value + 1, stateNonce) : stateNonce;
        }
    }
}