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
using Lachain.Storage.State;
using Lachain.Utility;

namespace Lachain.Core.Blockchain.Pool
{
    public class TransactionPool : ITransactionPool
    {
        private static readonly ILogger<TransactionPool> Logger = LoggerFactory.GetLoggerForClass<TransactionPool>();

        private readonly ITransactionVerifier _transactionVerifier;
        private readonly IPoolRepository _poolRepository;
        private readonly ITransactionManager _transactionManager;
        private readonly IBlockManager _blockManager;
        private readonly INonceCalculator _nonceCalculator;
        private readonly IStateManager _stateManager;

        private readonly ConcurrentDictionary<UInt256, TransactionReceipt> _transactions
            = new ConcurrentDictionary<UInt256, TransactionReceipt>();
        private readonly ConcurrentDictionary<ulong, List<TransactionReceipt>> _proposed 
            = new ConcurrentDictionary<ulong, List<TransactionReceipt>>();
        private IList<TransactionReceipt> _toDeleteRepo = new List<TransactionReceipt>();
        private ulong _lastSanitized = 0;
        private ISet<TransactionReceipt> _transactionsQueue;

        public event EventHandler<TransactionReceipt>? TransactionAdded;
        public IReadOnlyDictionary<UInt256, TransactionReceipt> Transactions => _transactions;

        public TransactionPool(
            ITransactionVerifier transactionVerifier,
            IPoolRepository poolRepository,
            ITransactionManager transactionManager,
            IBlockManager blockManager,
            INonceCalculator nonceCalculator,
            IStateManager stateManager
        )
        {
            _transactionVerifier = transactionVerifier;
            _poolRepository = poolRepository;
            _transactionManager = transactionManager;
            _blockManager = blockManager;
            _nonceCalculator = nonceCalculator;
            _stateManager = stateManager;
            _transactionsQueue = new HashSet<TransactionReceipt>();

            _blockManager.OnBlockPersisted += OnBlockPersisted;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        private void OnBlockPersisted(object sender, Block block)
        {
            SanitizeMemPool(block.Header.Index);
            // TO DO: we should make this removal async for better performance
            _poolRepository.RemoveTransactions(_toDeleteRepo.Select(receipt => receipt.Hash));
            _toDeleteRepo.Clear();
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
            List<UInt256> transactionsToRemove = new List<UInt256>();
            foreach (var txHash in txHashes)
            {
                Logger.LogTrace($"Tx from pool: {txHash.ToHex()}");
                var tx = _poolRepository.GetTransactionByHash(txHash);
                
                if (tx is null)
                    continue;
                if(Add(tx, false) != OperatingError.Ok)
                    transactionsToRemove.Add(tx.Hash);
            }
            _poolRepository.RemoveTransactions(transactionsToRemove);
            CheckConsistency();
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

        [MethodImpl(MethodImplOptions.Synchronized)]
        public OperatingError Add(TransactionReceipt receipt, bool notify = true)
        {
            if (receipt is null)
                throw new ArgumentNullException(nameof(receipt));

            /* don't add to transaction pool transactions with the same hashes */
            if (_transactions.ContainsKey(receipt.Hash))
                return OperatingError.AlreadyExists;
            /* verify transaction before adding */
            if (!(GetNextNonceForAddress(receipt.Transaction.From) == receipt.Transaction.Nonce))
                return OperatingError.InvalidNonce;
            
            /* special case for system transactions */
            if (receipt.Transaction.From.IsZero())
            {
                if (!_poolRepository.ContainsTransactionByHash(receipt.Hash))
                    _poolRepository.AddTransaction(receipt);
                return OperatingError.Ok;
            }

            /* check if the address has enough gas */ 
            if(!IsBalanceValid(receipt))
                return OperatingError.InsufficientBalance;

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

            _nonceCalculator.TryAdd(receipt);
            Logger.LogTrace($"Added transaction {receipt.Hash.ToHex()} to pool");
            if (notify) TransactionAdded?.Invoke(this, receipt);
            return OperatingError.Ok;
        }
        private bool IsGovernanceTx(TransactionReceipt receipt)
        {
            return receipt.Transaction.To == ContractRegisterer.GovernanceContract;
        }

        private bool IsBalanceValid(TransactionReceipt receipt)
        {
            var balance = _stateManager.LastApprovedSnapshot.Balances.GetBalance(receipt.Transaction.From);
            var fee = new Money(receipt.Transaction.GasLimit * receipt.Transaction.GasPrice);
            return balance.CompareTo(fee) >= 0;
        }

        // this sanitizeMemPool is required to be done after persisting a block and before proposing
        // transactions for the next block and it should be called only once for better performance
        // after persisting a block - two methods can be called - (1) onBlockPersisted and
        // (2) Peek and their order may vary.
        // that means it's possible that peek is executed before onBlockPersisted and vice versa
        // We keep track of the lastSanitized era and do the sanitization only once per era

        private void SanitizeMemPool(ulong era)
        {
            if(era <= _lastSanitized) return;
            if(_lastSanitized != 0 && _lastSanitized + 1 != era) {
                Logger.LogError($"_lastSanitized: {_lastSanitized}, trying to sanitize: {era}");
                throw new Exception("Pool Sanitization should be sequential");
            }

            // after persisting the block - first remove the proposed
            // transactions from the nonceCalculator and db pool
            // this makes sure that the transactions that failed during
            // emulation is removed from _nonceCalculator

            if(_proposed.TryGetValue(era, out var lastProposed))
            {
                foreach(var receipt in lastProposed)
                {
                    _nonceCalculator.TryRemove(receipt);
                    _toDeleteRepo.Add(receipt);
                }
                _proposed.Remove(era, out var _);
            }

            // make sure that - transactions with bad nonces
            // and not enough balance are removed
            
            var wasTransactionsQueue = _transactionsQueue.Count;

            var toErase = new List<TransactionReceipt>();
            var nextNonce = new Dictionary<UInt160, ulong>();

            // order the transactions 
            var orderedTransactions = _transactionsQueue.OrderBy(x => x, new ReceiptComparer());

            foreach(var receipt in orderedTransactions)
            {
                var address = receipt.Transaction.From;
                if(!nextNonce.ContainsKey(receipt.Transaction.From))
                    nextNonce.Add(address, _transactionManager.CalcNextTxNonce(address));

                if(receipt.Transaction.Nonce != nextNonce[address] || !IsBalanceValid(receipt))
                    toErase.Add(receipt);
                else
                    nextNonce[address]++;
            }

            foreach(var receipt in toErase)
            {
                _transactionsQueue.Remove(receipt);
                _nonceCalculator.TryRemove(receipt);
                _transactions.TryRemove(receipt.Hash, out var _);
                _toDeleteRepo.Add(receipt);
            }

            if (wasTransactionsQueue != _transactionsQueue.Count)
            {
                Logger.LogTrace(
                    $"Sanitized mempool; dropped {wasTransactionsQueue - _transactionsQueue.Count} txs"
                );
            }

            _lastSanitized = era;
            CheckConsistency();
            return;
        }
        private IReadOnlyCollection<TransactionReceipt> Take(HashSet<UInt256> txHashesTaken, ulong era)
        {
            List<TransactionReceipt> result = new List<TransactionReceipt>();
            foreach(var hash in txHashesTaken)
            {
                if(!_transactions.ContainsKey(hash)) 
                    throw new Exception("Transaction does not exist in the _transaction hashset");
                
                result.Add(_transactions[hash]);
            }

            if(_proposed.ContainsKey(era)) 
            {
                Logger.LogError("Asking for transactions for era {era} more than once");
                throw new Exception("Proposing transactions multiple times for same era");
            }
            _proposed.TryAdd(era, new List<TransactionReceipt>(result));
            
            foreach(var tx in result)
            {
                _transactionsQueue.Remove(tx);
                bool canErase = _transactions.TryRemove(tx.Hash, out var _);
                if(canErase is false)
                    throw new Exception("Transaction does not exist in _transaction");
            }

            if(_transactions.Count != _transactionsQueue.Count)
            {
                // this should never happen, something must be wrong if this gets triggered
                Logger.LogError($"_transaction.Count = {_transactions.Count} is not equal to _transactionsQueue.Count = {_transactionsQueue.Count}");
            }
            return result;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public IReadOnlyCollection<TransactionReceipt> Peek(int txsToLook, int txsToTake, ulong era)
        {
            Logger.LogTrace($"Proposing Transactions from pool");
            // try sanitizing mempool ...
            SanitizeMemPool(era - 1);

            var rnd = new Random();
            HashSet<UInt256> takenTxHashes = new HashSet<UInt256>();

            // take governance transaction from transaction queue
            foreach(var receipt in _transactionsQueue)
            {
                if(!IsGovernanceTx(receipt))
                    continue;
                var hash = receipt.Hash;
                if(takenTxHashes.Contains(hash) || !_transactions.ContainsKey(hash) || _transactionManager.GetByHash(hash) != null)
                    continue;
                takenTxHashes.Add(hash);
            }
            if(takenTxHashes.Count >= txsToTake)
                return Take(takenTxHashes, era);

            // We first greedily take some most profitable transactions. Let's group by sender and
            // peek the best by gas price (so we do not break nonce order)
            var txsBySender = new Dictionary<UInt160, List<TransactionReceipt>>();
            var orderedTransactionsQueue = _transactionsQueue.OrderBy(x => x, new ReceiptComparer());
            foreach(var receipt in orderedTransactionsQueue)
            {
                if(IsGovernanceTx(receipt))
                    continue;
                var hash = receipt.Hash;
                if(takenTxHashes.Contains(hash) || !_transactions.ContainsKey(hash) || _transactionManager.GetByHash(hash) != null)
                    continue;

                if(txsBySender.ContainsKey(receipt.Transaction.From))
                    txsBySender[receipt.Transaction.From].Add(receipt);
                else
                    txsBySender.Add(receipt.Transaction.From, new List<TransactionReceipt>{receipt});
            }

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

            int alreadyTakenCount = takenTxHashes.Count;
            for (var i = 0; i < txsToTake - alreadyTakenCount && txsBySender.Count > 0; ++i)
            {
                var key = rnd.SelectRandom(txsBySender.Keys);
                var txsFrom = txsBySender[key];
                var tx = txsFrom.Last();
                takenTxHashes.Add(tx.Hash);
                txsFrom.RemoveAt(txsFrom.Count - 1);
                if (txsFrom.Count == 0) txsBySender.Remove(key);
            }

            return Take(takenTxHashes, era);
        }

        private void CheckConsistency()
        {
            // sanity check that transactions and transactionsQueue
            // are the same          
            if(_transactions.Count() != _transactionsQueue.Count())
            {
                Logger.LogError($"_transactions count: {_transactions.Count()} != _transactionsQueue: {_transactionsQueue.Count()}");
                throw new Exception("transactions and transactionQueue should be of same size");
            }

            // at this point nonce-calculator and transaction should have the 
            // same set of transactions

            if(_transactions.Count() != _nonceCalculator.Count())
            {
                Logger.LogError($"_transactions count: {_transactions.Count()} != _nonceCalculator: {_nonceCalculator.Count()}");
                throw new Exception("transactions and nonceCalculator should be of same size");
            }

            // Proposed should be empty
            if(_proposed.Count() != 0)
            {
                Logger.LogError($"_proposed size: {_proposed.Count()} but should be empty.");
                throw new Exception("proposed dict should be empty");
            }
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public uint Size()
        {
            return (uint) _transactionsQueue.Count;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Clear()
        {
            _transactions.Clear();
            _transactionsQueue.Clear();
            _nonceCalculator.Clear();
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void ClearRepository()
        {
            _poolRepository.ClearPool();
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public ulong GetNextNonceForAddress(UInt160 address)
        {
            var poolNonce = _nonceCalculator.GetMaxNonceForAddress(address);
            var stateNonce = _transactionManager.CalcNextTxNonce(address);
            return poolNonce.HasValue ? Math.Max(poolNonce.Value + 1, stateNonce) : stateNonce;
        }
    }
}