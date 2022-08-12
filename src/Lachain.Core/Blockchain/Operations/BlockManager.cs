using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text;
using Lachain.Core.Blockchain.Error;
using Lachain.Core.Blockchain.Genesis;
using Lachain.Core.Blockchain.Hardfork;
using Lachain.Core.Blockchain.Interface;
using Lachain.Core.Blockchain.SystemContracts.ContractManager;
using Lachain.Core.Blockchain.VM;
using Lachain.Core.Config;
using Lachain.Crypto;
using Lachain.Crypto.ECDSA;
using Lachain.Crypto.Misc;
using Lachain.Logger;
using Lachain.Proto;
using Lachain.Storage.Repositories;
using Lachain.Storage.State;
using Lachain.Utility;
using Lachain.Utility.Utils;
using Prometheus;
using Lachain.Core.Blockchain.SystemContracts.Utils;
using Lachain.Core.Blockchain.SystemContracts.Storage;
using Lachain.Core.Blockchain.Validators;
using Lachain.Utility.Serialization;


namespace Lachain.Core.Blockchain.Operations
{
    /* 
        BlockManager is the class that handles the block emulation and execution. 
        It also provides some basic functions to find block by height, latestBlock etc.
        
        Block execution happens in exactly one of the two following ways : 
        
        (1) if the node is validator and participates in consensus, during the end of consensus 
        the transaction-set and their order for the next block is chosen. The node first emulates
        all these transactions (removing all invalid transactions), create the blockHeader which
        also includes stateHash and waits for confirmations from other peers. 
        After it's confirmed, the block is produced and executed. 

        (2) blockSynchronizer class continuously queries other peers - their latest block. If it finds
        a new block, it executes the block immediately using this class's execution.  

    */
    public class BlockManager : IBlockManager
    {
        private static readonly ILogger<BlockManager> Logger = LoggerFactory.GetLoggerForClass<BlockManager>();
        private static readonly ICrypto Crypto = CryptoProvider.GetCrypto();

        // _heightCache and _heightCacheQueue are part of cache layer for storing the recent blocks
        // _heightCache is a dictionary that keeps (height, block) for fast retrieval of blocks for
        // a given height.
        private IDictionary<ulong, Block> _heightCache;

        // _heightCacheQueue is a queue that keeps all the heights in the cache to 
        // find the oldest height to remove from the cache as the cache size exceeds _blockSizeLimit
        private Queue<ulong> _heightCacheQueue;

        private static readonly Counter BlockExecCounter = Metrics.CreateCounter(
            "lachain_block_exec_count",
            "Number of times that block execution was called",
            "mode"
        );

        private static readonly Summary BlockExecTime = Metrics.CreateSummary(
            "lachain_block_exec_duration_seconds",
            "Duration of block execution for last 5 minutes",
            new SummaryConfiguration
            {
                MaxAge = TimeSpan.FromMinutes(5),
                LabelNames = new[] {"mode"},
                Objectives = new[]
                {
                    new QuantileEpsilonPair(0.95, 0.05),
                    new QuantileEpsilonPair(0.5, 0.05)
                }
            }
        );

        private static readonly Gauge BlockHeight = Metrics.CreateGauge(
            "lachain_latest_block",
            "Index of latest block in blockchain",
            new GaugeConfiguration
            {
                SuppressInitialValue = true
            }
        );

        private static readonly Summary BlockTime = Metrics.CreateSummary(
            "lachain_block_time_seconds",
            "Time between consecutive blocks",
            new SummaryConfiguration
            {
                SuppressInitialValue = true
            }
        );
        
        private static readonly Summary BlockSize = Metrics.CreateSummary(
            "lachain_block_size_bytes",
            "Block size",
            new SummaryConfiguration
            {
                SuppressInitialValue = true
            }
        );
        
        private static readonly Summary TxSize = Metrics.CreateSummary(
            "lachain_tx_size_bytes",
            "Transaction size",
            new SummaryConfiguration
            {
                SuppressInitialValue = true
            }
        );
        
        private static readonly Summary TxInBlock = Metrics.CreateSummary(
            "lachain_tx_in_block",
            "Count of tx per block",
            new SummaryConfiguration
            {
                SuppressInitialValue = true
            }
        );

        private readonly ITransactionManager _transactionManager;
        private readonly IGenesisBuilder _genesisBuilder;
        private readonly IMultisigVerifier _multisigVerifier;
        private readonly IStateManager _stateManager;
        private readonly ISnapshotIndexRepository _snapshotIndexRepository;
        private readonly IConfigManager _configManager;
        private readonly ILocalTransactionRepository _localTransactionRepository;
        private InvocationContext? _contractTxJustExecuted;

        public event EventHandler<InvocationContext>? OnSystemContractInvoked;

        // default cache size limit is 100 items
        private int _blockSizeLimit = 100;

        public BlockManager(
            ITransactionManager transactionManager,
            IGenesisBuilder genesisBuilder,
            IMultisigVerifier multisigVerifier,
            IStateManager stateManager,
            ISnapshotIndexRepository snapshotIndexRepository,
            IConfigManager configManager,
            ILocalTransactionRepository localTransactionRepository
        )
        {
            _transactionManager = transactionManager;
            _genesisBuilder = genesisBuilder;
            _multisigVerifier = multisigVerifier;
            _stateManager = stateManager;
            _snapshotIndexRepository = snapshotIndexRepository;
            _configManager = configManager;
            _localTransactionRepository = localTransactionRepository;
            _transactionManager.OnSystemContractInvoked += TransactionManagerOnSystemContractInvoked;

            var cacheConfig = _configManager.GetConfig<CacheConfig>("cache");
            if (cacheConfig != null && cacheConfig.BlockHeight != null && cacheConfig.BlockHeight.SizeLimit != null)
            {
                _blockSizeLimit = cacheConfig.BlockHeight.SizeLimit.Value;
            }

            _heightCache = new Dictionary<ulong, Block?>(_blockSizeLimit);
            _heightCacheQueue = new Queue<ulong>(_blockSizeLimit);
        }

        // _contractTxJustExecuted Keeps track of the most recent contract transaction
        // that was executed. This is especially useful to trigger key generation process
        private void TransactionManagerOnSystemContractInvoked(object sender, InvocationContext e)
        {
            _contractTxJustExecuted = e;
        }

        public event EventHandler<Block>? OnBlockPersisted;

        // gets the most recent block's height
        [MethodImpl(MethodImplOptions.Synchronized)]
        public ulong GetHeight()
        {
            return _stateManager.LastApprovedSnapshot.Blocks.GetTotalBlockHeight();
        }

        // gets the most recent block 
        public Block LatestBlock()
        {
            return GetByHeight(GetHeight()) ?? throw new InvalidOperationException("No blocks in blockchain");
        }

        // Gets the block with a given height, returns null if the
        // block with the given height can't be found
        // We have FIFO cache layer to make these queries faster. When a new block is executed and
        // commited to the database, we add this block to the cache and erase if the cache is size 
        // exceeds _blockSizeLimit.
        [MethodImpl(MethodImplOptions.Synchronized)]
        public Block? GetByHeight(ulong blockHeight)
        {
            // query from cache first
            if(_heightCache.TryGetValue(blockHeight, out var block))
                return block;
            
            // if not found in cache, query from storage
            return _stateManager.LastApprovedSnapshot.Blocks.GetBlockByHeight(blockHeight);
        }

        // Every block in the chain contains a hash. Given a hash, it returns the block.
        // returns null if no block with this hash is found 
        [MethodImpl(MethodImplOptions.Synchronized)]
        public Block? GetByHash(UInt256 blockHash)
        {
            return _stateManager.LastApprovedSnapshot.Blocks.GetBlockByHash(blockHash);
        }

        // Checks if a block is the genesis block (the very first block that has height = 0)
        // the check is done by comparing the hash of genesis block and the given block
        [MethodImpl(MethodImplOptions.Synchronized)]
        private bool _IsGenesisBlock(Block block)
        {
            return block.Hash.Equals(_genesisBuilder.Build().Block.Hash);
        }

        // This method emulates a set of transactions in a block. When during consensus, the transactions
        // are chosen for the next block, some of the transactions might be invalid (as any validator may be malicious
        // and its proposed transaction might be invalid). So, before execution, a validator emulates these 
        // transactions to find all the invalid transactions. It then picks all the valid transactions and 
        // adds them to the block and executes that block 
        [MethodImpl(MethodImplOptions.Synchronized)]
        public Tuple<OperatingError, List<TransactionReceipt>, UInt256, List<TransactionReceipt>> Emulate(
            Block block, IEnumerable<TransactionReceipt> transactions
        )
        {
            // No two threads can be in the safeContext at the same time. 
            // Always execute / emulate inside safeContext. this makes sure that no two threads
            // concurrently writes to the chain. 
            var (operatingError, removeTransactions, stateHash, relayTransactions) = _stateManager.SafeContext(() =>
            {
                // this is the state before the start of emulation
                var snapshotBefore = _stateManager.LastApprovedSnapshot;
                Logger.LogTrace("Executing transactions in no-check no-commit mode");
                // during emulation, we _Execute the block, but does not commit the changes to 
                // the database as it's not the permament change
                var error = _Execute(
                    block,
                    transactions,
                    out var removedTransactions,
                    out var relayedTransactions,
                    true,
                    out var gasUsed,
                    out _
                );
                // for testing purpose only
                Logger.LogInformation($"{removedTransactions.Count} txes failed in emulation");
                // currentStateHash represents the stateHash just after the changes to the chain
                // due to these transactions that were valid and executed
                var currentStateHash = _stateManager.LastApprovedSnapshot.StateHash;
                Logger.LogDebug(
                    $"Block execution successful, height={_stateManager.LastApprovedSnapshot.Blocks.GetTotalBlockHeight()}" +
                    $" stateHash={currentStateHash.ToHex()}, gasUsed={gasUsed}"
                );
                // we rollback to the state before emulation
                // thus emulation does not change the state
                _stateManager.RollbackTo(snapshotBefore);
                return Tuple.Create(error, removedTransactions, currentStateHash, relayedTransactions);
            });
            return Tuple.Create(operatingError, removeTransactions, stateHash, relayTransactions);
        }

        // this method is called just after a new block is committed to the 
        // database and thus is persisted. After a block is persisted, this new block is added
        // to the cache
        [MethodImpl(MethodImplOptions.Synchronized)]
        private void BlockPersisted(Block block)
        {
            _transactionManager.ClearProcessedTransactions();
            _snapshotIndexRepository.SaveSnapshotForBlock(block.Header.Index, _stateManager.LastApprovedSnapshot);
            // a new block is committed to the storage

            // add to the cache
            if(_heightCache.TryAdd(block.Header.Index, block))
                _heightCacheQueue.Enqueue(block.Header.Index);

            // if cache size exceeds limit, remove the oldest one
            if(_heightCacheQueue.Count > _blockSizeLimit)
            {
                // remove oldest height from queue
                var oldestKey = _heightCacheQueue.Dequeue();

                // remove from cache dictionary
                _heightCache.Remove(oldestKey);
            }
            
            // this invocation triggers consensusManager and consensusManager knows that a new block
            // is added and thus stops the current era of consensus if it's running. It also triggers a 
            // method in pool and pool does some necessary clean-ups
            OnBlockPersisted?.Invoke(this, block);
        }

        // This is the method that is called to add a new block in the chain. It can be called from 2 classes. 
        // (1) BlockProducer: If a block is approved after consensus, BlockProducer adds that new block to the chain
        // by executing it. (2) BlockSynchronizer: BlockSynchronizer keeps on querying for new blocks from its peers. When
        // it gets a new block, it adds to the chain by executing. 
        [MethodImpl(MethodImplOptions.Synchronized)]
        public OperatingError Execute(Block block, IEnumerable<TransactionReceipt> transactionsEnumerable, bool checkStateHash,
            bool commit)
        {
            // No two threads can be in the safeContext at the same time. 
            // Always execute / emulate inside safeContext. this makes sure that no two threads
            // concurrently writes to the chain. 

            var error = _stateManager.SafeContext(() =>
            {
                var transactions = transactionsEnumerable.ToList();
                var snapshotBefore = _stateManager.LastApprovedSnapshot;
                var startTime = TimeUtils.CurrentTimeMillis();
                var operatingError = _Execute(
                    block, transactions, out var removed, out _, false, out var gasUsed, out var totalFee
                );
                Logger.LogInformation($"{removed.Count} txes failed in execution");
                if (operatingError != OperatingError.Ok)
                {
                    Logger.LogError($"Error occured while executing block: {operatingError}");
                    return operatingError;
                }

                if (checkStateHash && !_stateManager.LastApprovedSnapshot.StateHash.Equals(block.Header.StateHash))
                {
                    Logger.LogError(
                        $"Cannot execute block {block.Hash.ToHex()} " +
                        $"with stateHash={block.Header.StateHash.ToHex()} specified in header," +
                        $"since computed state hash is {_stateManager.LastApprovedSnapshot.StateHash.ToHex()}, " +
                        $"stack trace is {new System.Diagnostics.StackTrace()}");

                    _stateManager.RollbackTo(snapshotBefore);
                    return OperatingError.InvalidStateHash;
                }

                /* flush changes to database */
                if (!commit)
                    return OperatingError.Ok;
                // TODO: this is hack to avoid concurrency issues, one more save will be done in BlockPersisted() call
                _snapshotIndexRepository.SaveSnapshotForBlock(block.Header.Index, _stateManager.LastApprovedSnapshot);
                if (block.Header.Index > 0)
                {
                    var blockTime = block.Timestamp -
                                    _stateManager.LastApprovedSnapshot.Blocks.GetBlockByHeight(block.Header.Index - 1)!
                                        .Timestamp;
                    BlockTime.Observe(blockTime / 1000.0);
                    BlockHeight.Set(block.Header.Index);
                    BlockSize.Observe(block.CalculateSize());
                    foreach (var transactionReceipt in transactions)
                    {
                        TxSize.Observe(transactionReceipt.Transaction.RlpWithSignature(transactionReceipt.Signature,  HardforkHeights.IsHardfork_9Active(block.Header.Index)).Count());
                    }
                    TxInBlock.Observe(block.TransactionHashes.Count);

                    Logger.LogInformation(
                        $"New block {block.Header.Index} with hash {block.Hash.ToHex()}, " +
                        $"txs {block.TransactionHashes.Count} in {TimeUtils.CurrentTimeMillis() - startTime}ms, " +
                        $"gas used {gasUsed}, fee {totalFee}. " +
                        $"Since last block: {blockTime} ms"
                    );
                }
                _stateManager.Commit();
                BlockPersisted(block);
                return OperatingError.Ok;
            });
            return error;
        }

        // This is the internal method used both by Execute and Emulate
        [MethodImpl(MethodImplOptions.Synchronized)]
        private OperatingError _Execute(
            Block block,
            IEnumerable<TransactionReceipt> transactions,
            out List<TransactionReceipt> removeTransactions,
            out List<TransactionReceipt> relayTransactions,
            bool isEmulation,
            out ulong gasUsed,
            out Money totalFee
        )
        {
            var mode = isEmulation ? "emulate" : "commit";
            using var timer = BlockExecTime.WithLabels(mode).NewTimer();
            BlockExecCounter.WithLabels(mode).Inc();
            totalFee = Money.Zero;
            gasUsed = 0;
            _contractTxJustExecuted = null;

            var currentTransactions = transactions
                .ToDictionary(tx => tx.Hash, tx => tx);

            removeTransactions = new List<TransactionReceipt>();
            relayTransactions = new List<TransactionReceipt>();

            /* verify next block */
            var error = Verify(block);
            if (error != OperatingError.Ok)
                return error;

            /* check next block index */
            var currentBlockHeader = _stateManager.LastApprovedSnapshot.Blocks.GetTotalBlockHeight();
            if (!_IsGenesisBlock(block) && currentBlockHeader + 1 != block.Header.Index)
            {
                Logger.LogError($"Error executing block {block.Header.Index}: latest block is {currentBlockHeader}");
                return OperatingError.InvalidNonce;
            }

            var exists = _stateManager.LastApprovedSnapshot.Blocks.GetBlockByHeight(block.Header.Index);
            if (exists != null)
                return OperatingError.BlockAlreadyExists;

            /* check prev block hash */
            var latestBlock = _stateManager.LastApprovedSnapshot.Blocks.GetBlockByHeight(currentBlockHeader);
            if (latestBlock != null && !block.Header.PrevBlockHash.Equals(latestBlock.Hash))
                return OperatingError.PrevBlockHashMismatched;

            /* verify block signatures */
            error = VerifySignatures(block, false);
            if (error != OperatingError.Ok)
                return error;

            /* check do we have all transactions specified */
            if (block.TransactionHashes.Any(txHash => !currentTransactions.ContainsKey(txHash)))
            {
                return OperatingError.TransactionLost;
            }

            /* execute transactions */
            ulong indexInBlock = 0;
            foreach (var txHash in block.TransactionHashes)
            {
                Logger.LogTrace($"Trying to execute tx : {txHash.ToHex()}");
                /* try to find transaction by hash */
                var receipt = currentTransactions[txHash];
                if(receipt is null) Logger.LogError($"For tx : {txHash.ToHex()} receipt is NULL");
                receipt.Block = block.Header.Index;
                receipt.GasUsed = GasMetering.DefaultTxCost;
                receipt.IndexInBlock = indexInBlock;
                var transaction = receipt.Transaction;
                Logger.LogInformation($"tx : {txHash.ToHex()} blockHeaderIndex:{receipt.Block} indexinBlock:{receipt.IndexInBlock}");
            
                try
                {
                    // to make any changes to the state, it's required to
                    // (1) create a new snapshot, (2) make changes to the snapshot
                    // (3) either approve or rollback()
                    // approving adds all the changes to the lastApprovedSnapshot
                    // and rollback() discards all the changes and lastApprovedSnapshot is not changed
                    var snapshot = _stateManager.NewSnapshot();
                    // if the "from" address of the transaction does not have enough gas to
                    // pay for the transaction, then this transaction is not executed and thus skipped.
                    var gasLimitCheck = _CheckTransactionGasLimit(transaction, snapshot);
                    if (gasLimitCheck != OperatingError.Ok)
                    {
                        removeTransactions.Add(receipt);
                        _stateManager.Rollback();
                        Logger.LogWarning(
                            $"Unable to execute transaction {txHash.ToHex()} with nonce ({transaction.Nonce}): not enough balance for gas"
                        );
                        continue;
                    }
                    else Logger.LogInformation($"Gas limit is ok for tx : {txHash.ToHex()}");

                    /* try to execute transaction */
                    OperatingError result = OperatingError.Ok;
                    try
                    {
                        result = _transactionManager.Execute(block, receipt, snapshot);
                    }
                    catch (Exception e)
                    {
                        Logger.LogWarning($"Exception during tx execution: {e}");
                        result = OperatingError.InvalidContract;
                    }
                    var txFailed = result != OperatingError.Ok;
                    if (txFailed)
                    {
                        _stateManager.Rollback();
                        // what exactly is nonce of an transaction ? 
                        // nonce represents the label of the transactions from a particular address
                        // for example - let's say address A has 3 transactions in the chain.
                        // their nonces must be 0, 1, 2
                        // if A sends a new transaction, it's nonce must be 3. 
                        // if the nonce does not match with the expected nonce, this transaction
                        // is also removed and skipped
                        if (result == OperatingError.InvalidNonce)
                        {
                            removeTransactions.Add(receipt);
                            Logger.LogWarning(
                                $"Unable to execute transaction {txHash.ToHex()} with nonce ({transaction.Nonce}): invalid nonce"
                            );
                            continue;
                        }

                        snapshot = _stateManager.NewSnapshot();
                        // Adds this transaction to the Transactions Snapshot
                        snapshot.Transactions.AddTransaction(receipt, TransactionStatus.Failed);
                        Logger.LogTrace($"Transaction {txHash.ToHex()} failed because of error: {result}");
                    }
                    else Logger.LogInformation($"Tx is not failed for tx : {txHash.ToHex()}");

                    /* check block gas limit after execution */
                    gasUsed += receipt.GasUsed;
                    if (gasUsed > GasMetering.DefaultBlockGasLimit)
                    {
                        removeTransactions.Add(receipt);
                        relayTransactions.Add(receipt);
                        _stateManager.Rollback();
                        /* this should never happen cuz that mean that someone applied overflowed block */
                        if (!isEmulation)
                            throw new InvalidBlockException(OperatingError.BlockGasOverflow);
                        Logger.LogWarning(
                            $"Unable to take transaction {txHash.ToHex()} with gas {receipt.GasUsed}, block gas limit overflowed {gasUsed}/{GasMetering.DefaultBlockGasLimit}");
                        continue;
                    }
                    else Logger.LogInformation($"Block gas limit after execution ok for tx : {txHash.ToHex()}");

                    /* try to take fee from sender */
                    result = _TakeTransactionFee(receipt, snapshot, out var fee);
                    if (result != OperatingError.Ok)
                    {
                        removeTransactions.Add(receipt);
                        _stateManager.Rollback();
                        Logger.LogWarning(
                            $"Unable to execute transaction {txHash.ToHex()} with nonce ({transaction.Nonce}), cannot take fee due to {result}"
                        );
                        continue;
                    }
                    else Logger.LogInformation($"Fee taken for tx : {txHash.ToHex()}");

                    totalFee += fee;

                    if (!txFailed)
                    {
                        /* mark transaction as executed */
                        Logger.LogTrace($"Transaction executed {txHash.ToHex()}");
                        snapshot.Transactions.AddTransaction(receipt, TransactionStatus.Executed);
                    }

                    _stateManager.Approve();
                    indexInBlock++;
                }
                catch (Exception ex)
                {
                    Logger.LogWarning($"Exception [{ex}] while executing tx {txHash.ToHex()}");
                    removeTransactions.Add(receipt);
                    if (_stateManager.PendingSnapshot != null)
                        _stateManager.Rollback();
                }

                if (_contractTxJustExecuted != null && !isEmulation)
                {
                    try
                    {
                        // this invocation is required to trigger key generation appropriately
                        OnSystemContractInvoked?.Invoke(this, _contractTxJustExecuted);
                    }
                    catch (Exception e)
                    {
                        Logger.LogError(
                            $"While executing block {block.Header.Index} exception occured while processing system contract call: {e}");
                    }
                    finally
                    {
                        _contractTxJustExecuted = null;
                        _localTransactionRepository.TryAddTransaction(receipt);
                    }
                }
            }

            block.GasPrice = _CalcEstimatedBlockFee(currentTransactions.Values);

            /* save block to repository */
            try
            {
                var snapshotBlock = _stateManager.NewSnapshot();
                snapshotBlock.Blocks.AddBlock(block);
                _stateManager.Approve();
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"Exception [{ex.ToString()}] while adding block tx");
                if (_stateManager.PendingSnapshot != null)
                    _stateManager.Rollback();
            }


            return OperatingError.Ok;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        private OperatingError _CheckTransactionGasLimit(Transaction transaction, IBlockchainSnapshot snapshot)
        {
            /* check available LA balance */
            var fee = new Money(transaction.GasLimit * transaction.GasPrice);
            return snapshot.Balances.GetBalance(transaction.From).CompareTo(fee) < 0
                ? OperatingError.InsufficientBalance
                : OperatingError.Ok;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        private OperatingError _CheckTransactionGasPrice(Transaction transaction, IBlockchainSnapshot snapshot)
        {
            return transaction.GasPrice >= snapshot.NetworkGasPrice || transaction.From.IsZero()
                ? OperatingError.Ok
                : OperatingError.Underpriced;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        private OperatingError _TakeTransactionFee(
            TransactionReceipt transaction, IBlockchainSnapshot snapshot, out Money fee
        )
        {
            fee = new Money(transaction.GasUsed * transaction.Transaction.GasPrice);
            /* transfer fee from wallet to validator */
            if (fee == Money.Zero) return OperatingError.Ok;

            /* check available LA balance */
            var senderBalance = snapshot.Balances.GetBalance(transaction.Transaction.From);
            if (senderBalance < fee)
            {
                return OperatingError.InsufficientBalance;
            }

            return !snapshot.Balances.TransferBalance(transaction.Transaction.From,
                ContractRegisterer.GovernanceContract, fee)
                ? OperatingError.InsufficientBalance
                : OperatingError.Ok;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public Signature Sign(BlockHeader block, EcdsaKeyPair keyPair)
        {
            return Crypto.SignHashed(
                block.Keccak().ToBytes(), keyPair.PrivateKey.Encode(),
                HardforkHeights.IsHardfork_9Active(block.Index)
            ).ToSignature(HardforkHeights.IsHardfork_9Active(block.Index));
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public OperatingError VerifySignature(BlockHeader blockHeader, Signature signature, ECDSAPublicKey publicKey)
        {
            var result = Crypto.VerifySignatureHashed(
                blockHeader.Keccak().ToBytes(), signature.Encode(), publicKey.EncodeCompressed(),
                HardforkHeights.IsHardfork_9Active(blockHeader.Index)
            );
            return result ? OperatingError.Ok : OperatingError.InvalidSignature;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public OperatingError VerifySignatures(Block block, bool checkValidatorSet)
        {
            if (!block.Header.Keccak().Equals(block.Hash))
                return OperatingError.HashMismatched;
            if (_IsGenesisBlock(block))
                return OperatingError.Ok;
            if (checkValidatorSet && !VerifyValidatorSet(block.Multisig.Validators, block.Header.Index - 1))
                return OperatingError.InvalidMultisig;
            return _multisigVerifier.VerifyMultisig(block.Multisig, block.Hash, HardforkHeights.IsHardfork_9Active(block.Header.Index));
        }

        private bool VerifyValidatorSet(IReadOnlyCollection<ECDSAPublicKey> keys, ulong height)
        {
            try
            {
                IReadOnlyCollection<ECDSAPublicKey> validators = _snapshotIndexRepository.GetSnapshotForBlock(height).Validators
                    .GetValidatorsPublicKeys().ToArray();
                var result = validators.All(v => keys.Contains(v)) && keys.All(k => validators.Contains(k));
                if (!result)
                {
                    Logger.LogDebug("Validator set from peer block does not match with validator set from snapshot");
                }
                return result;
            }
            catch (Exception exception)
            {
                Logger.LogTrace($"Got exception while matching validator set: {exception}");
                return false;
            }
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public OperatingError Verify(Block block)
        {
            var header = block.Header;
            if (!Equals(block.Hash, header.Keccak()))
                return OperatingError.HashMismatched;
            if (block.Header.Index != 0 && header.PrevBlockHash.IsZero())
                return OperatingError.InvalidBlock;
            if (header.MerkleRoot is null)
                return OperatingError.InvalidMerkeRoot;
            var merkleRoot = MerkleTree.ComputeRoot(block.TransactionHashes) ?? UInt256Utils.Zero;
            if (!merkleRoot.Equals(header.MerkleRoot))
                return OperatingError.InvalidMerkeRoot;
            return OperatingError.Ok;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        private static ulong _CalcEstimatedBlockFee(IEnumerable<TransactionReceipt> txs)
        {
            var txsArray = txs as TransactionReceipt[] ?? txs.ToArray();
            if (txsArray.Length == 0)
                return 0;
            var sum = txsArray.Aggregate(0UL, (current, tx) => current + tx.GasUsed * tx.Transaction.GasPrice);
            return sum / (ulong) txsArray.Length;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public ulong CalcEstimatedFee(UInt256 blockHash)
        {
            var block = _stateManager.LastApprovedSnapshot.Blocks.GetBlockByHash(blockHash) ??
                        throw new InvalidOperationException();
            if (block.GasPrice != 0)
                return block.GasPrice;
            var txs = block.TransactionHashes.SelectMany(txHash =>
            {
                var tx = _transactionManager.GetByHash(txHash);
                return tx is null ? Enumerable.Empty<TransactionReceipt>() : new[] {tx};
            });
            return _CalcEstimatedBlockFee(txs);
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public ulong CalcEstimatedFee()
        {
            var currentHeight = _stateManager.LastApprovedSnapshot.Blocks.GetTotalBlockHeight();
            var block = _stateManager.LastApprovedSnapshot.Blocks.GetBlockByHeight(currentHeight) ??
                        throw new InvalidOperationException();
            if (block.GasPrice != 0)
                return block.GasPrice;
            var txs = block.TransactionHashes.SelectMany(txHash =>
            {
                var tx = _transactionManager.GetByHash(txHash);
                return tx is null ? Enumerable.Empty<TransactionReceipt>() : new[] {tx};
            });
            return _CalcEstimatedBlockFee(txs);
        }

        // Every time a node starts, it first tries to build the genesis block
        public bool TryBuildGenesisBlock()
        {
            // genesis block is built from the config.json file
            // genesis block mints tokens to the validators for the first cycle
            var genesisBlock = _genesisBuilder.Build();

            // if genesis block can already be found, we return immediately
            if (_stateManager.LastApprovedSnapshot.Blocks.GetBlockByHeight(0) != null)
                return false;
            var snapshot = _stateManager.NewSnapshot();
            var genesisConfig = _configManager.GetConfig<GenesisConfig>("genesis");
            if (genesisConfig is null) return false;
            genesisConfig.ValidateOrThrow();
            var fakeVerificationKeys = Enumerable.Range(0, genesisConfig.Validators.Count)
                .Select(i => genesisConfig.ThresholdEncryptionPublicKey.HexToBytes())
                .ToArray();
            var initialConsensusState = new ConsensusState(
                genesisConfig.ThresholdEncryptionPublicKey.HexToBytes(),
                fakeVerificationKeys, 
                genesisConfig.Validators.Select(v => new ValidatorCredentials
                (
                    v.EcdsaPublicKey.HexToBytes().ToPublicKey(),
                    v.ThresholdSignaturePublicKey.HexToBytes()
                )).ToArray()
            );
            snapshot.Validators.SetConsensusState(initialConsensusState, false);

            // stake delegation happens even before genesis block
            // stake delegation means - some other address stakes for the validators
            // config.json keeps the stakerAddress and the stakeAmount for each of the validators

            // init system contracts storage
            var dummyStakerPub = new string('f', CryptoUtils.PublicKeyLength * 2).HexToBytes();
            snapshot.Storage.SetRawValue(
                ContractRegisterer.StakingContract,
                new BigInteger(6).ToUInt256().Buffer,
                dummyStakerPub
            );

            // TODO: get rid of explicit numbering of fields
            var initialVrfSeed = Encoding.ASCII.GetBytes("test");
            snapshot.Storage.SetRawValue(
                ContractRegisterer.StakingContract,
                new BigInteger(7).ToUInt256().Buffer,
                initialVrfSeed
            );

            var initialBlockReward = Money.Parse(genesisConfig.BlockReward).ToUInt256().ToBytes();
            snapshot.Storage.SetRawValue(
                ContractRegisterer.GovernanceContract,
                new BigInteger(3).ToUInt256().Buffer,
                initialBlockReward
            );

            var initialBasicGasPrice = Money.Parse(genesisConfig.BasicGasPrice).ToUInt256().ToBytes();
            snapshot.Storage.SetRawValue(
                ContractRegisterer.GovernanceContract,
                new BigInteger(8).ToUInt256().Buffer,
                initialBasicGasPrice
            );

            // The followings are the variables used in stakingContract
            // This variables are stored in the storage snapshot and is a part of the chain
            // To understand the what each variables represent, refer to StakingContract.cs 

            // We do the stake delegation even before the execution of genesis block

            var _userToStake = new StorageMapping(
                ContractRegisterer.StakingContract,
                snapshot.Storage,
                new BigInteger(3).ToUInt256()
            );
            var _stakers = new StorageVariable(
                ContractRegisterer.StakingContract,
                snapshot.Storage,
                new BigInteger(6).ToUInt256()
            );
            var _userToPubKey = new StorageMapping(
               ContractRegisterer.StakingContract,
               snapshot.Storage,
               new BigInteger(2).ToUInt256()
            );
            var _pubKeyToStaker = new StorageMapping(
                ContractRegisterer.StakingContract,
                snapshot.Storage,
                new BigInteger(12).ToUInt256()
            );
            var _userToStartCycle = new StorageMapping(
                ContractRegisterer.StakingContract,
                snapshot.Storage,
                new BigInteger(4).ToUInt256()
            );

            foreach (var validator in genesisConfig.Validators)
            {
                if(validator.StakeAmount == null || validator.StakerAddress == null) continue;
                var validatorPublicKey = validator.EcdsaPublicKey.HexToBytes();
                var validatorAddress = Hepler.PublicKeyToAddress(validatorPublicKey).ToBytes();
                var stakerAddress = validator.StakerAddress.HexToBytes();

                // add balance to staking contract
                var stakeAmount = Money.Parse(validator.StakeAmount);
                snapshot.Balances.AddBalance(ContractRegisterer.StakingContract, stakeAmount, true);
                // set stake value 
                _userToStake.SetValue(validatorAddress, stakeAmount.ToUInt256().ToBytes());
                // update stakers list
                var stakers = _stakers.Get();
                _stakers.Set(stakers.Concat(validatorPublicKey).ToArray());
                // user to public key and public key to staker
                _userToPubKey.SetValue(validatorAddress, validatorPublicKey);
                _pubKeyToStaker.SetValue(validatorPublicKey, stakerAddress);
                // set start cycle
                _userToStartCycle.SetValue(validatorAddress, BitConverter.GetBytes(0));
            }

            _stateManager.Approve();

            // emulate and execute the genesis block
            var (error, removeTransactions, stateHash, relayTransactions) =
                Emulate(genesisBlock.Block, genesisBlock.Transactions);
            if (error != OperatingError.Ok) throw new InvalidBlockException(error);
            if (removeTransactions.Count != 0) throw new InvalidBlockException(OperatingError.InvalidTransaction);
            if (relayTransactions.Count != 0) throw new InvalidBlockException(OperatingError.InvalidTransaction);
            genesisBlock.Block.Header.StateHash = stateHash;
            genesisBlock.Block.Hash = genesisBlock.Block.Header.Keccak();

            error = Execute(genesisBlock.Block, genesisBlock.Transactions, commit: true, checkStateHash: true);
            if (error != OperatingError.Ok) throw new InvalidBlockException(error);
            _stateManager.Commit();
            BlockPersisted(genesisBlock.Block);
            return true;
        }
    }
}