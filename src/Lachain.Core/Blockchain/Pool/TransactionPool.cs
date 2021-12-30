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
using Lachain.Storage.State;
using Lachain.Utility;
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
        private readonly IStateManager _stateManager;

        private readonly ConcurrentDictionary<UInt256, TransactionReceipt> _transactions
            = new ConcurrentDictionary<UInt256, TransactionReceipt>();

        private readonly ConcurrentDictionary<UInt160, List<TransactionReceipt>> _transactionPerAddress
            = new ConcurrentDictionary<UInt160, List<TransactionReceipt>>();

        private IList<TransactionReceipt> _lastProposed = new List<TransactionReceipt>();

        private ISet<TransactionReceipt> _transactionsQueue;
        private ISet<TransactionReceipt> _relayQueue;

        public event EventHandler<TransactionReceipt>? TransactionAdded;
        public IReadOnlyDictionary<UInt256, TransactionReceipt> Transactions => _transactions;

        public TransactionPool(
            ITransactionVerifier transactionVerifier,
            IPoolRepository poolRepository,
            ITransactionManager transactionManager,
            IBlockManager blockManager,
            IStateManager stateManager
        )
        {
            _transactionVerifier = transactionVerifier;
            _poolRepository = poolRepository;
            _transactionManager = transactionManager;
            _blockManager = blockManager;
            _transactionsQueue = new HashSet<TransactionReceipt>();
            _relayQueue = new HashSet<TransactionReceipt>();
            _stateManager = stateManager;

            _blockManager.OnBlockPersisted += OnBlockPersisted;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        private void OnBlockPersisted(object sender, Block e)
        {
            Sanitize();
            foreach (var txHash in e.TransactionHashes)
            {
                Delete(txHash);
            }
            _poolRepository.RemoveTransactions(e.TransactionHashes);
            SanitizePool();

            foreach(var receipt in _lastProposed)
            {
                RemoveFromTransactionPerAddress(receipt);
            }
            _lastProposed.Clear();
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
            Logger.LogTrace($"restoring transactions from pool to in-memory storage");
            foreach (var txHash in txHashes)
            {
                Logger.LogTrace($"Tx from pool: {txHash.ToHex()}");
                var tx = _poolRepository.GetTransactionByHash(txHash);
                
                if (tx is null)
                    continue;
                Add(tx);
            }
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public IEnumerable<UInt256> GetTransactionPoolRepository()
        {
            return _poolRepository.GetTransactionPool();
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public OperatingError Add(Transaction transaction, Signature signature, bool notify = true)
        {
            var acceptedTx = new TransactionReceipt
            {
                Transaction = transaction,
                Hash = transaction.FullHash(signature),
                Signature = signature,
                Status = TransactionStatus.Pool
            };
            return Add(acceptedTx, notify);
        }

        private void UpdateNonceForAddress(UInt160 address, TransactionReceipt receipt)
        {
            if(_transactionPerAddress.TryGetValue(address, out var list)) 
            {
                if(!list.Contains(receipt))
                    list.Add(receipt);
            }
            else 
            {
                _transactionPerAddress.TryAdd(address, new List<TransactionReceipt>{receipt});
            }
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public OperatingError Add(TransactionReceipt receipt, bool notify = true)
        {
         
            if (receipt is null)
                throw new ArgumentNullException(nameof(receipt));

            /* don't add to transaction pool transactions with the same hashes */
            if (_transactions.ContainsKey(receipt.Hash))
                return OperatingError.AlreadyExists;
            /* verify transaction before adding */
            if (!(GetNextNonceForAddress(receipt.Transaction.From) >= receipt.Transaction.Nonce && TxNonceValid(receipt)))
                return OperatingError.InvalidNonce;

            /* special case for system transactions */
            if (receipt.Transaction.From.IsZero())
            {
                if (!_poolRepository.ContainsTransactionByHash(receipt.Hash))
                    _poolRepository.AddTransaction(receipt);
                return OperatingError.Ok;
            }

            //check if balance from "from" address is > tx.gasPrice * tx.gasLimit
            var address = receipt.Transaction.From;
            var balance = _stateManager.LastApprovedSnapshot.Balances.GetBalance(address);
            var fee = new Money(receipt.Transaction.GasLimit * receipt.Transaction.GasPrice);

            if (balance.CompareTo(fee) < 0)
            {
                return OperatingError.InsufficientBalance;
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

            UpdateNonceForAddress(receipt.Transaction.From, receipt);
            Logger.LogTrace($"Added transaction {receipt.Hash.ToHex()} to pool");
            if (notify) TransactionAdded?.Invoke(this, receipt);
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

            var toErase = new List<TransactionReceipt>();
            foreach(var receipt in _relayQueue)
                if(!TxNonceValid(receipt))
                    toErase.Add(receipt);
            
            foreach(var receipt in toErase)
            {
                _relayQueue.Remove(receipt);
                RemoveFromTransactionPerAddress(receipt);
                _transactions.TryRemove(receipt.Hash, out var _);
            }
            
            toErase.Clear();

            foreach(var receipt in _transactionsQueue)
                if(!TxNonceValid(receipt))
                    toErase.Add(receipt);
            
            foreach(var receipt in toErase)
            {
                _transactionsQueue.Remove(receipt);
                RemoveFromTransactionPerAddress(receipt);
                _transactions.TryRemove(receipt.Hash, out var _);
            }

            if (wasRelayQueueSize != _relayQueue.Count || wasTransactionsQueue != _transactionsQueue.Count)
            {
                Logger.LogTrace(
                    $"Sanitized mempool; dropped {wasTransactionsQueue - _transactionsQueue.Count} txs" +
                    $" from queue & {wasRelayQueueSize - _relayQueue.Count} tx from relay queue"
                );
            }
        }


        private void SanitizePool()
        {
            var txHashes = _poolRepository.GetTransactionPool();
            var txToRemove = new List<UInt256>();
            foreach(var txHash in txHashes)
            {
                var tx = _poolRepository.GetTransactionByHash(txHash);
                if(tx is null) 
                    continue;
                if(!TxNonceValid(tx))
                    txToRemove.Add(txHash);
            }
            int txRemovedCount = _poolRepository.RemoveTransactions(txToRemove);
            
            if(txRemovedCount > 0) 
            {
                Logger.LogTrace($"Sanitized transaction pool; dropped {txRemovedCount} txs from pool repository");
            }
        }

        private void RemoveFromTransactionPerAddress(TransactionReceipt receipt) 
        {
            var from = receipt.Transaction.From;
            if(_transactionPerAddress.TryGetValue(from, out var list))
            {
                list.Remove(receipt);
                if(list.Count == 0)
                {
                    _transactionPerAddress.TryRemove(from, out var _);
                }
            }
        }



        private void RemoveTxes(IReadOnlyCollection<TransactionReceipt> txes)
        {
            foreach (var tx in txes)
            {
                _transactionsQueue.Remove(tx);
                _relayQueue.Remove(tx);
                _lastProposed.Add(tx);
            }
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public IReadOnlyCollection<TransactionReceipt> Peek(int txsToLook, int txsToTake)
        {
            Logger.LogTrace($"Proposing Transactions from pool");
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
                RemoveFromTransactionPerAddress(tx);
            }
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Clear()
        {
            _transactions.Clear();
            _transactionsQueue.Clear();
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void ClearRepository()
        {
            _poolRepository.ClearPool();
        }

        


        [MethodImpl(MethodImplOptions.Synchronized)]
        public ulong? GetMaxNonceForAddress(UInt160 address)
        {
            if (!_transactionPerAddress.TryGetValue(address, out var list)) return null;
            
            ulong nonce = 0;
            foreach(var receipt in list)
            {
                nonce = Math.Max(nonce, receipt.Transaction.Nonce);
            }
            return nonce;
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