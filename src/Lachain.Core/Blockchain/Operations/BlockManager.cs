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
                LabelNames = new []{"mode"},
                Objectives = new[]
                {
                    new QuantileEpsilonPair(0.95, 0.05),
                    new QuantileEpsilonPair(0.5, 0.05)
                }
            }
        );

        private static readonly Gauge BlockHeight = Metrics.CreateGauge(
            "lachain_latest_block",
            "Index of latest block in blockchain"
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
            BlockHeight.Set(block.Header.Index);            
            _snapshotIndexRepository.SaveSnapshotForBlock(block.Header.Index, _stateManager.LastApprovedSnapshot);
            OnBlockPersisted?.Invoke(this, block);
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public OperatingError Execute(Block block, IEnumerable<TransactionReceipt> transactions, bool checkStateHash,
            bool commit)
        {
            var error = _stateManager.SafeContext(() =>
            {
                var snapshotBefore = _stateManager.LastApprovedSnapshot;
                var startTime = TimeUtils.CurrentTimeMillis();
                var operatingError = _Execute(
                    block, transactions, out _, out _, false, out var gasUsed, out var totalFee
                );
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
                    Logger.LogInformation(
                        $"New block {block.Header.Index} with hash {block.Hash.ToHex()}, " +
                        $"txs {block.TransactionHashes.Count} in {TimeUtils.CurrentTimeMillis() - startTime}ms, " +
                        $"gas used {gasUsed}, fee {totalFee}. " +
                        $"Since last block: " +
                        $"{block.Timestamp - _stateManager.LastApprovedSnapshot.Blocks.GetBlockByHeight(block.Header.Index - 1)!.Timestamp} ms"
                    );
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
            error = VerifySignatures(block);
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
                receipt.Block = block.Header.Index;
                receipt.GasUsed = GasMetering.DefaultTxCost;
                receipt.IndexInBlock = indexInBlock;
                var transaction = receipt.Transaction;
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

                /* try to execute transaction */
                var result = _transactionManager.Execute(block, receipt, snapshot);
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
                    snapshot.Transactions.AddTransaction(receipt, TransactionStatus.Failed);
                    Logger.LogTrace($"Transaction {txHash.ToHex()} failed because of error: {result}");
                }

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

                totalFee += fee;

                if (!txFailed)
                {
                    /* mark transaction as executed */
                    Logger.LogTrace($"Transaction executed {txHash.ToHex()}");
                    snapshot.Transactions.AddTransaction(receipt, TransactionStatus.Executed);
                }

                _stateManager.Approve();
                indexInBlock++;
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
            var snapshotBlock = _stateManager.NewSnapshot();
            snapshotBlock.Blocks.AddBlock(block);
            _stateManager.Approve();

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
        public OperatingError VerifySignatures(Block block)
        {
            if (!block.Header.Keccak().Equals(block.Hash))
                return OperatingError.HashMismatched;
            if (_IsGenesisBlock(block))
                return OperatingError.Ok;
            return _multisigVerifier.VerifyMultisig(block.Multisig, block.Hash);
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