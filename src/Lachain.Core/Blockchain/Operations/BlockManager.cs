using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text;
using Lachain.Core.Blockchain.Error;
using Lachain.Core.Blockchain.Genesis;
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
using Lachain.Storage.Trie;


namespace Lachain.Core.Blockchain.Operations
{
    public class BlockManager : IBlockManager
    {
        private static readonly ILogger<BlockManager> Logger = LoggerFactory.GetLoggerForClass<BlockManager>();
        private static readonly ICrypto Crypto = CryptoProvider.GetCrypto();

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

        private ulong _lastTouchedBlock = 0;

        public event EventHandler<InvocationContext>? OnSystemContractInvoked;

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
        }

        private void TransactionManagerOnSystemContractInvoked(object sender, InvocationContext e)
        {
            _contractTxJustExecuted = e;
        }

        public event EventHandler<Block>? OnBlockPersisted;

        [MethodImpl(MethodImplOptions.Synchronized)]
        public ulong GetHeight()
        {
            return _stateManager.LastApprovedSnapshot.Blocks.GetTotalBlockHeight();
        }

        public Block LatestBlock()
        {
            return GetByHeight(GetHeight()) ?? throw new InvalidOperationException("No blocks in blockchain");
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public Block? GetByHeight(ulong blockHeight)
        {
            return _stateManager.LastApprovedSnapshot.Blocks.GetBlockByHeight(blockHeight);
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public Block? GetByHash(UInt256 blockHash)
        {
            return _stateManager.LastApprovedSnapshot.Blocks.GetBlockByHash(blockHash);
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        private bool _IsGenesisBlock(Block block)
        {
            return block.Hash.Equals(_genesisBuilder.Build().Block.Hash);
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public Tuple<OperatingError, List<TransactionReceipt>, UInt256, List<TransactionReceipt>> Emulate(
            Block block, IEnumerable<TransactionReceipt> transactions
        )
        {
            var (operatingError, removeTransactions, stateHash, relayTransactions) = _stateManager.SafeContext(() =>
            {
                var snapshotBefore = _stateManager.LastApprovedSnapshot;

                Logger.LogTrace($"Doing touch operations before emulation");

                foreach(var receipt in transactions)
                {
                    snapshotBefore.Transactions.AddToTouch(receipt);
                    snapshotBefore.Balances.AddToTouch(receipt);
                    snapshotBefore.Events.AddToTouch(receipt);
                    snapshotBefore.Contracts.AddToTouch(receipt);
                }

                snapshotBefore.Transactions.TouchAll();
                snapshotBefore.Balances.TouchAll();
                snapshotBefore.Events.TouchAll();
                snapshotBefore.Contracts.TouchAll();

                Logger.LogTrace($"Trying some queries");

                foreach(var receipt in transactions)
                {
                    snapshotBefore.Balances.GetBalance(receipt.Transaction.From);
                    snapshotBefore.Balances.GetBalance(receipt.Transaction.To);
                    snapshotBefore.Transactions.GetTransactionByHash(receipt.Hash);
                }
                     

                Logger.LogTrace($"second time touching");

                foreach(var receipt in transactions)
                {
                    snapshotBefore.Transactions.AddToTouch(receipt);
                    snapshotBefore.Balances.AddToTouch(receipt);
                    snapshotBefore.Events.AddToTouch(receipt);
                    snapshotBefore.Contracts.AddToTouch(receipt);
                }

                snapshotBefore.Transactions.TouchAll();
                snapshotBefore.Balances.TouchAll();
                snapshotBefore.Events.TouchAll();
                snapshotBefore.Contracts.TouchAll();

                _lastTouchedBlock = block.Header.Index;

                TrieHashMap.databaseCounter = 0;

                Logger.LogTrace("Executing transactions in no-check no-commit mode");
                var error = _Execute(
                    block,
                    transactions,
                    out var removedTransactions,
                    out var relayedTransactions,
                    true,
                    out var gasUsed,
                    out _
                );

                Logger.LogTrace($"During Emulation database hit count: {TrieHashMap.databaseCounter}");
                TrieHashMap.databaseCounter = 0;

                var currentStateHash = _stateManager.LastApprovedSnapshot.StateHash;
                
                Logger.LogDebug(
                    $"Block execution successful, height={_stateManager.LastApprovedSnapshot.Blocks.GetTotalBlockHeight()}" +
                    $" stateHash={currentStateHash.ToHex()}, gasUsed={gasUsed}"
                );
                _stateManager.RollbackTo(snapshotBefore);
                return Tuple.Create(error, removedTransactions, currentStateHash, relayedTransactions);
            });
            return Tuple.Create(operatingError, removeTransactions, stateHash, relayTransactions);
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        private void BlockPersisted(Block block)
        {
            _snapshotIndexRepository.SaveSnapshotForBlock(block.Header.Index, _stateManager.LastApprovedSnapshot);
            OnBlockPersisted?.Invoke(this, block);
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public OperatingError Execute(Block block, IEnumerable<TransactionReceipt> transactionsEnumerable, bool checkStateHash,
            bool commit)
        {
            var error = _stateManager.SafeContext(() =>
            {
                var transactions = transactionsEnumerable.ToList();
                var snapshotBefore = _stateManager.LastApprovedSnapshot;

                if(block.Header.Index != _lastTouchedBlock) 
                {
                    Logger.LogTrace($"Doing touch operations before execution");

                    foreach(var receipt in transactions)
                    {
                        snapshotBefore.Transactions.AddToTouch(receipt);
                        snapshotBefore.Balances.AddToTouch(receipt);
                        snapshotBefore.Events.AddToTouch(receipt);
                        snapshotBefore.Contracts.AddToTouch(receipt);
                    }

                    snapshotBefore.Transactions.TouchAll();
                    snapshotBefore.Balances.TouchAll();
                    snapshotBefore.Events.TouchAll();
                    snapshotBefore.Contracts.TouchAll();

                    Logger.LogTrace($"Ended touch operations before execution");

                    _lastTouchedBlock = block.Header.Index;
                }
                
                TrieHashMap.databaseCounter = 0;

                var startTime = TimeUtils.CurrentTimeMillis();
                var operatingError = _Execute(
                    block, transactions, out _, out _, false, out var gasUsed, out var totalFee
                );

                Logger.LogTrace($"During execution database hit count: {TrieHashMap.databaseCounter}");
                TrieHashMap.databaseCounter = 0;

            

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
                        TxSize.Observe(transactionReceipt.Transaction.RlpWithSignature(transactionReceipt.Signature).Count());
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
                    var snapshot = _stateManager.NewSnapshot();
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
                        if (result == OperatingError.InvalidNonce)
                        {
                            removeTransactions.Add(receipt);
                            Logger.LogWarning(
                                $"Unable to execute transaction {txHash.ToHex()} with nonce ({transaction.Nonce}): invalid nonce"
                            );
                            continue;
                        }

                        snapshot = _stateManager.NewSnapshot();
                        Logger.LogTrace("before adding tx");
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

                    Logger.LogTrace($"After taking fee");
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
                        Logger.LogTrace($"Adding transaction to database");
                        snapshot.Transactions.AddTransaction(receipt, TransactionStatus.Executed);
                        Logger.LogTrace($"Transaction executed {txHash.ToHex()}");
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
            Logger.LogTrace("Before taking fee");
            fee = new Money(transaction.GasUsed * transaction.Transaction.GasPrice);
            /* transfer fee from wallet to validator */
            if (fee == Money.Zero) return OperatingError.Ok;

            Logger.LogTrace($"fee is not zero");
            Logger.LogTrace($"sender: {transaction.Transaction.From.ToHex()}, receiver: {transaction.Transaction.To.ToHex()}");
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
                block.Keccak().ToBytes(), keyPair.PrivateKey.Encode()
            ).ToSignature();
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public OperatingError VerifySignature(BlockHeader blockHeader, Signature signature, ECDSAPublicKey publicKey)
        {
            var result = Crypto.VerifySignatureHashed(
                blockHeader.Keccak().ToBytes(), signature.Encode(), publicKey.EncodeCompressed()
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
            return _multisigVerifier.VerifyMultisig(block.Multisig, block.Hash);
        }

        private bool VerifyValidatorSet(IReadOnlyCollection<ECDSAPublicKey> keys, ulong height)
        {
            try
            {
                IReadOnlyCollection<ECDSAPublicKey> validators = _snapshotIndexRepository.GetSnapshotForBlock(height).Validators
                    .GetValidatorsPublicKeys().ToArray();
                return validators.All(v => keys.Contains(v)) && keys.All(k => validators.Contains(k));
            }
            catch (Exception e)
            {
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

        public bool TryBuildGenesisBlock()
        {
            var genesisBlock = _genesisBuilder.Build();
            if (_stateManager.LastApprovedSnapshot.Blocks.GetBlockByHeight(0) != null)
                return false;
            var snapshot = _stateManager.NewSnapshot();
            var genesisConfig = _configManager.GetConfig<GenesisConfig>("genesis");
            if (genesisConfig is null) return false;
            genesisConfig.ValidateOrThrow();
            var initialConsensusState = new ConsensusState(
                genesisConfig.ThresholdEncryptionPublicKey.HexToBytes(),
                genesisConfig.Validators.Select(v => new ValidatorCredentials
                (
                    v.EcdsaPublicKey.HexToBytes().ToPublicKey(),
                    v.ThresholdSignaturePublicKey.HexToBytes()
                )).ToArray()
            );
            snapshot.Validators.SetConsensusState(initialConsensusState);

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