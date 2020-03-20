using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Phorkus.Core.Blockchain.Genesis;
using Phorkus.Core.Blockchain.Validators;
using Phorkus.Core.Utils;
using Phorkus.Core.VM;
using Phorkus.Crypto;
using Phorkus.Logger;
using Phorkus.Proto;
using Phorkus.Storage.Repositories;
using Phorkus.Storage.State;
using Phorkus.Utility;
using Phorkus.Utility.Utils;

namespace Phorkus.Core.Blockchain.OperationManager
{
    public class BlockManager : IBlockManager
    {
        private readonly ITransactionManager _transactionManager;
        private readonly ICrypto _crypto = CryptoProvider.GetCrypto();
        private readonly IValidatorManager _validatorManager;
        private readonly IGenesisBuilder _genesisBuilder;
        private readonly IMultisigVerifier _multisigVerifier;
        private readonly ILogger<BlockManager> _logger = LoggerFactory.GetLoggerForClass<BlockManager>();
        private readonly IStateManager _stateManager;
        private readonly ISnapshotIndexRepository _snapshotIndexRepository;

        public BlockManager(
            ITransactionManager transactionManager,
            IValidatorManager validatorManager,
            IGenesisBuilder genesisBuilder,
            IMultisigVerifier multisigVerifier,
            IStateManager stateManager,
            ISnapshotIndexRepository snapshotIndexRepository
        )
        {
            _transactionManager = transactionManager;
            _validatorManager = validatorManager;
            _genesisBuilder = genesisBuilder;
            _multisigVerifier = multisigVerifier;
            _stateManager = stateManager;
            _snapshotIndexRepository = snapshotIndexRepository;
        }

        public event EventHandler<Block>? OnBlockPersisted;
        public event EventHandler<Block>? OnBlockSigned;

        public Block? GetByHeight(ulong blockHeight)
        {
            return _stateManager.LastApprovedSnapshot.Blocks.GetBlockByHeight(blockHeight);
        }

        public Block? GetByHash(UInt256 blockHash)
        {
            return _stateManager.LastApprovedSnapshot.Blocks.GetBlockByHash(blockHash);
        }

        private bool _IsGenesisBlock(Block block)
        {
            return block.Hash.Equals(_genesisBuilder.Build().Block.Hash);
        }

        public Tuple<OperatingError, List<TransactionReceipt>, UInt256, List<TransactionReceipt>> Emulate(
            Block block, IEnumerable<TransactionReceipt> transactions
        )
        {
            var (operatingError, removeTransactions, stateHash, relayTransactions) = _stateManager.SafeContext(() =>
            {
                var snapshotBefore = _stateManager.LastApprovedSnapshot;
                _logger.LogDebug("Executing transactions in no-check no-commit mode");
                var error = _Execute(
                    block,
                    transactions,
                    out var removedTransactions,
                    out var relayedTransactions,
                    false,
                    out var gasUsed,
                    out _);
                if (error != OperatingError.Ok)
                    throw new InvalidOperationException($"Cannot assemble block, {error}");
                var currentStateHash = _stateManager.LastApprovedSnapshot.StateHash;
                _logger.LogDebug(
                    $"Execution successful, height={_stateManager.LastApprovedSnapshot.Blocks.GetTotalBlockHeight()}" +
                    $" stateHash={currentStateHash.ToHex()}, gasUsed={gasUsed}"
                );
                _stateManager.RollbackTo(snapshotBefore);
                _logger.LogDebug(
                    $"Rolled back to height {_stateManager.LastApprovedSnapshot.Blocks.GetTotalBlockHeight()}"
                );
                return Tuple.Create(error, removedTransactions, currentStateHash, relayedTransactions);
            });
            return Tuple.Create(operatingError, removeTransactions, stateHash, relayTransactions);
        }

        public void BlockPersisted(Block block)
        {
            _snapshotIndexRepository.SaveSnapshotForBlock(block.Header.Index, _stateManager.LastApprovedSnapshot);
            OnBlockPersisted?.Invoke(this, block);
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public OperatingError Execute(Block block, IEnumerable<TransactionReceipt> transactions, bool checkStateHash,
            bool commit)
        {
            return _stateManager.SafeContext(() =>
            {
                var snapshotBefore = _stateManager.LastApprovedSnapshot;
                var startTime = TimeUtils.CurrentTimeMillis();
                var operatingError = _Execute(block, transactions, out _, out _, true, out var gasUsed,
                    out var totalFee);
                if (operatingError != OperatingError.Ok)
                {
                    _logger.LogError($"Error occured while executing block: {operatingError}");
                    throw new InvalidBlockException(operatingError);
                }

                if (checkStateHash && !_stateManager.LastApprovedSnapshot.StateHash.Equals(block.Header.StateHash))
                {
                    _stateManager.RollbackTo(snapshotBefore);
                    return OperatingError.InvalidStateHash;
                }

                /* flush changes to database */
                if (!commit)
                    return OperatingError.Ok;
                _logger.LogInformation(
                    $"New block {block.Header.Index} with hash {block.Hash.ToHex()}, txs {block.TransactionHashes.Count} in {TimeUtils.CurrentTimeMillis() - startTime} ms, gas used {gasUsed}, fee {totalFee}");
                _snapshotIndexRepository.SaveSnapshotForBlock(block.Header.Index, _stateManager.LastApprovedSnapshot); // TODO: this is hack
                _stateManager.Commit();
                BlockPersisted(block);
                return OperatingError.Ok;
            });
        }

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
                _logger.LogError($"Error executing block {block.Header.Index}: latest block is {currentBlockHeader}");
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
            foreach (var txHash in block.TransactionHashes)
            {
                /* try to find transaction by hash */
                var transaction = currentTransactions[txHash];
                transaction.GasUsed = GasMetering.DefaultTxTransferGasCost;
                var snapshot = _stateManager.NewSnapshot();

                var gasLimitCheck = _CheckTransactionGasLimit(transaction.Transaction, snapshot);
                if (gasLimitCheck != OperatingError.Ok)
                {
                    removeTransactions.Add(transaction);
                    _stateManager.Rollback();
                    _logger.LogWarning(
                        $"Unable to execute transaction {txHash.ToHex()} with nonce ({transaction.Transaction?.Nonce}): not enough balance for gas"
                    );
                    continue;
                }

                /* try to execute transaction */
                var result = _transactionManager.Execute(block, transaction, snapshot);
                if (result != OperatingError.Ok)
                {
                    _stateManager.Rollback();
                    if (result == OperatingError.InvalidNonce)
                    {
                        removeTransactions.Add(transaction);
                        _logger.LogWarning(
                            $"Unable to execute transaction {txHash.ToHex()} with nonce ({transaction.Transaction?.Nonce}): invalid nonce"
                        );
                    }
                    else
                    {
                        snapshot = _stateManager.NewSnapshot();
                        snapshot.Transactions.AddTransaction(transaction, TransactionStatus.Failed);
                        _stateManager.Approve();
                    }

                    continue;
                }

                /* check block gas limit after execution */
                gasUsed += transaction.GasUsed;
                if (gasUsed > GasMetering.DefaultBlockGasLimit)
                {
                    removeTransactions.Add(transaction);
                    relayTransactions.Add(transaction);
                    _stateManager.Rollback();
                    /* this should never happen cuz that mean that someone applied overflowed block */
                    if (!isEmulation)
                        throw new InvalidBlockException(OperatingError.BlockGasOverflow);
                    _logger.LogWarning(
                        $"Unable to take transaction {txHash.Buffer.ToHex()} with gas {transaction.GasUsed}, block gas limit overflowed {gasUsed}/{GasMetering.DefaultBlockGasLimit}");
                    continue;
                }

                /* try to take fee from sender */
                result = _TakeTransactionFee((long) block.Header.Index, transaction, snapshot, out var fee);
                if (result != OperatingError.Ok)
                {
                    removeTransactions.Add(transaction);
                    _stateManager.Rollback();
                    _logger.LogWarning(
                        $"Unable to execute transaction {txHash.Buffer.ToHex()} with nonce ({transaction.Transaction?.Nonce}), {result}");
                    continue;
                }

                totalFee += fee;

                /* mark transaction as executed */
                _logger.LogDebug(
                    $"Successfully executed transaction {txHash.Buffer.ToHex()} with nonce ({transaction.Transaction.Nonce})");
                snapshot.Transactions.AddTransaction(transaction, TransactionStatus.Executed);
                _stateManager.Approve();
            }

            block.GasPrice = _CalcEstimatedBlockFee(currentTransactions.Values);

            /* save block to repository */
            var snapshotBlock = _stateManager.NewSnapshot();
            snapshotBlock.Blocks.AddBlock(block);
            _stateManager.Approve();

            return OperatingError.Ok;
        }

        private OperatingError _CheckTransactionGasLimit(Transaction transaction, IBlockchainSnapshot snapshot)
        {
            /* check available LA balance */
            var fee = new Money(transaction.GasLimit * transaction.GasPrice);
            return snapshot.Balances.GetBalance(transaction.From).CompareTo(fee) < 0
                ? OperatingError.InsufficientBalance
                : OperatingError.Ok;
        }

        private OperatingError _TakeTransactionFee(
            long block, TransactionReceipt transaction, IBlockchainSnapshot snapshot, out Money fee
        )
        {
            /* check available LA balance */
            fee = new Money(transaction.GasUsed * transaction.Transaction.GasPrice);
            /* transfer fee from wallet to validator */
            if (fee == Money.Zero) return OperatingError.Ok;

            // block - 1 because current block is only mined now and uses old validators
            var n = _validatorManager.GetValidators(block - 1).N;
            var sharedFee = fee / n;
            return _validatorManager.GetValidatorsPublicKeys(block - 1)
                .Any(validator =>
                    !snapshot.Balances.TransferBalance(transaction.Transaction.From,
                        _crypto.ComputeAddress(validator.Buffer.ToByteArray()).ToUInt160(), sharedFee))
                ? OperatingError.InsufficientBalance
                : OperatingError.Ok;
        }

        public Signature Sign(BlockHeader block, ECDSAKeyPair keyPair)
        {
            return _crypto.Sign(block.ToHash256().Buffer.ToByteArray(), keyPair.PrivateKey.Buffer.ToByteArray())
                .ToSignature();
        }

        public OperatingError VerifySignature(BlockHeader blockHeader, Signature signature, ECDSAPublicKey publicKey)
        {
            var result = _crypto.VerifySignature(blockHeader.ToHash256().Buffer.ToByteArray(),
                signature.Buffer.ToByteArray(), publicKey.Buffer.ToByteArray());
            return result ? OperatingError.Ok : OperatingError.InvalidSignature;
        }

        public OperatingError VerifySignatures(Block block)
        {
            if (!block.Header.ToHash256().Equals(block.Hash))
                return OperatingError.HashMismatched;
            if (_IsGenesisBlock(block))
                return OperatingError.Ok;
            return _multisigVerifier.VerifyMultisig(block.Multisig, block.Hash);
        }

        public OperatingError Verify(Block block)
        {
            var header = block.Header;
            if (!Equals(block.Hash, header.ToHash256()))
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

        private static ulong _CalcEstimatedBlockFee(IEnumerable<TransactionReceipt> txs)
        {
            var txsArray = txs as TransactionReceipt[] ?? txs.ToArray();
            if (txsArray.Length == 0)
                return 0;
            var sum = txsArray.Aggregate(0UL, (current, tx) => current + tx.GasUsed * tx.Transaction.GasPrice);
            return sum / (ulong) txsArray.Length;
        }

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
    }
}