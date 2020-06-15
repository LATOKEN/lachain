using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text;
using Google.Protobuf;
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

namespace Lachain.Core.Blockchain.Operations
{
    public class BlockManager : IBlockManager
    {
        private static readonly ILogger<BlockManager> Logger = LoggerFactory.GetLoggerForClass<BlockManager>();
        private static readonly ICrypto Crypto = CryptoProvider.GetCrypto();

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
            IConfigManager configManager, ILocalTransactionRepository localTransactionRepository)
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
                // Logger.LogDebug("Executing transactions in no-check no-commit mode");
                var error = _Execute(
                    block,
                    transactions,
                    out var removedTransactions,
                    out var relayedTransactions,
                    true,
                    out var gasUsed,
                    out _);
                if (error != OperatingError.Ok)
                    throw new InvalidOperationException($"Cannot assemble block, {error}");
                var currentStateHash = _stateManager.LastApprovedSnapshot.StateHash;
                // Logger.LogDebug(
                //     $"Execution successful, height={_stateManager.LastApprovedSnapshot.Blocks.GetTotalBlockHeight()}" +
                //     $" stateHash={currentStateHash.ToHex()}, gasUsed={gasUsed}"
                // );
                _stateManager.RollbackTo(snapshotBefore);
                // Logger.LogDebug(
                    // $"Rolled back to height {_stateManager.LastApprovedSnapshot.Blocks.GetTotalBlockHeight()}"
                // );
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
        public OperatingError Execute(Block block, IEnumerable<TransactionReceipt> transactions, bool checkStateHash,
            bool commit)
        {
            var error = _stateManager.SafeContext(() =>
            {
                var snapshotBefore = _stateManager.LastApprovedSnapshot;
                var startTime = TimeUtils.CurrentTimeMillis();
                var operatingError = _Execute(block, transactions, out _, out _, false, out var gasUsed,
                    out var totalFee);
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
                        $"since computed state hash is {_stateManager.LastApprovedSnapshot.StateHash.ToHex()}");
                    _stateManager.RollbackTo(snapshotBefore);
                    return OperatingError.InvalidStateHash;
                }

                /* flush changes to database */
                if (!commit)
                    return OperatingError.Ok;
                _snapshotIndexRepository.SaveSnapshotForBlock(block.Header.Index,
                    _stateManager.LastApprovedSnapshot); // TODO: this is hack
                // Logger.LogInformation(
                //     $"New block {block.Header.Index} with hash {block.Hash.ToHex()}, " +
                //     $"txs {block.TransactionHashes.Count} in {TimeUtils.CurrentTimeMillis() - startTime} ms, " +
                //     $"gas used {gasUsed}, fee {totalFee}"
                // );
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
            foreach (var (txHash, i) in block.TransactionHashes.Select((tx, i) => (tx, i)))
            {
                // Logger.LogError($"Trying to execute tx : {txHash.ToHex()}");
                /* try to find transaction by hash */
                var receipt = currentTransactions[txHash];
                receipt.Block = block.Header.Index;
                receipt.GasUsed = GasMetering.DefaultTxTransferGasCost;
                receipt.IndexInBlock = (ulong) i;
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
                    Logger.LogError($"Transaction {txHash.ToHex()} failed because of error: {result}");
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
                result = _TakeTransactionFee((long) block.Header.Index, receipt, snapshot, out var fee);
                if (result != OperatingError.Ok)
                {
                    removeTransactions.Add(receipt);
                    _stateManager.Rollback();
                    Logger.LogWarning(
                        $"Unable to execute transaction {txHash.ToHex()} with nonce ({transaction.Nonce}), {result}");
                    continue;
                }

                totalFee += fee;

                if (!txFailed)
                {
                    /* mark transaction as executed */
                    // Logger.LogDebug(
                    //     $"Transaction executed {txHash.ToHex()}");
                    snapshot.Transactions.AddTransaction(receipt, TransactionStatus.Executed);
                }

                _stateManager.Approve();
                if (_contractTxJustExecuted != null && !isEmulation)
                {
                    OnSystemContractInvoked?.Invoke(this, _contractTxJustExecuted);
                    _contractTxJustExecuted = null;
                    _localTransactionRepository.TryAddTransaction(receipt);
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
        private OperatingError _TakeTransactionFee(
            long block, TransactionReceipt transaction, IBlockchainSnapshot snapshot, out Money fee
        )
        {
            
            /* check available LA balance */
            fee = new Money(transaction.GasUsed * transaction.Transaction.GasPrice);
            /* transfer fee from wallet to validator */
            if (fee == Money.Zero) return OperatingError.Ok;

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
            return Crypto.Sign(block.KeccakBytes(), keyPair.PrivateKey.Encode())
                .ToSignature();
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public OperatingError VerifySignature(BlockHeader blockHeader, Signature signature, ECDSAPublicKey publicKey)
        {
            var result = Crypto.VerifySignature(
                blockHeader.KeccakBytes(), signature.Encode(), publicKey.EncodeCompressed()
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
            var initialConsensusState = new ConsensusState
            {
                TpkePublicKey = ByteString.CopyFrom(genesisConfig.ThresholdEncryptionPublicKey.HexToBytes())
            };
            initialConsensusState.Validators.Add(genesisConfig.Validators.Select(v => new ValidatorCredentials
            {
                PublicKey = v.EcdsaPublicKey.HexToBytes().ToPublicKey(),
                ResolvableAddress = v.ResolvableName,
                ThresholdSignaturePublicKey = ByteString.CopyFrom(v.ThresholdSignaturePublicKey.HexToBytes()),
            }));
            snapshot.Validators.SetConsensusState(initialConsensusState);
            
            // init system contracts storage 
            var dummyStakerPub = new string('f', CryptoUtils.PublicKeyLength * 2).HexToBytes();
            snapshot.Storage.SetRawValue(ContractRegisterer.StakingContract, new BigInteger(6).ToUInt256().Buffer, dummyStakerPub);
            
            var initialVrfSeed = Encoding.ASCII.GetBytes("test");
            snapshot.Storage.SetRawValue(ContractRegisterer.StakingContract, new BigInteger(7).ToUInt256().Buffer, initialVrfSeed);
            
            var initalBlockReward = Money.Parse(GenesisConfig.BlockReward).ToUInt256().ToBytes();
            snapshot.Storage.SetRawValue(ContractRegisterer.GovernanceContract, new BigInteger(3).ToUInt256().Buffer, initalBlockReward);
            
            _stateManager.Approve();
            var error = Execute(genesisBlock.Block, genesisBlock.Transactions, commit: true, checkStateHash: false);
            if (error != OperatingError.Ok) throw new InvalidBlockException(error);
            _stateManager.Commit();
            BlockPersisted(genesisBlock.Block);
            return true;
        }
    }
}