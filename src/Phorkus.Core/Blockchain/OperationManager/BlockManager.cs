using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using Phorkus.Core.Blockchain.Genesis;
using Phorkus.Core.Utils;
using Phorkus.Crypto;
using Phorkus.Proto;
using Phorkus.Storage.State;
using Phorkus.Utility;
using Phorkus.Utility.Utils;

namespace Phorkus.Core.Blockchain.OperationManager
{
    public class BlockManager : IBlockManager
    {
        private readonly ITransactionManager _transactionManager;
        private readonly ICrypto _crypto;
        private readonly IValidatorManager _validatorManager;
        private readonly IGenesisBuilder _genesisBuilder;
        private readonly IMultisigVerifier _multisigVerifier;
        private readonly Logger.ILogger<IBlockManager> _logger;
        private readonly IStateManager _stateManager;
        
        public BlockManager(
            ITransactionManager transactionManager,
            ICrypto crypto,
            IValidatorManager validatorManager,
            IGenesisBuilder genesisBuilder,
            IMultisigVerifier multisigVerifier, Logger.ILogger<IBlockManager> logger,
            IStateManager stateManager)
        {
            _transactionManager = transactionManager;
            _crypto = crypto;
            _validatorManager = validatorManager;
            _genesisBuilder = genesisBuilder;
            _multisigVerifier = multisigVerifier;
            _logger = logger;
            _stateManager = stateManager;
        }

        public event EventHandler<Block> OnBlockPersisted;
        public event EventHandler<Block> OnBlockSigned;

        public Block GetByHeight(ulong blockHeight)
        {
            return _stateManager.LastApprovedSnapshot.Blocks.GetBlockByHeight(blockHeight);
        }
        
        public Block GetByHash(UInt256 blockHash)
        {
            return _stateManager.LastApprovedSnapshot.Blocks.GetBlockByHash(blockHash);
        }

        private bool _IsGenesisBlock(Block block)
        {
            return block.Hash.Equals(_genesisBuilder.Build().Block.Hash);
        }

        public OperatingError EmulateExecution(Block block, IEnumerable<TransactionReceipt> transactions, out List<UInt256> removeTransactions)
        {
            throw new NotImplementedException();
        }

        public OperatingError Execute(Block block, IEnumerable<TransactionReceipt> transactions, bool checkStateHash, bool commit)
        {
            var currentTransactions = transactions.ToDictionary(tx => tx.Hash, tx => tx);
            var startTime = TimeUtils.CurrentTimeMillis();
            var snapshotBefore = _stateManager.LastApprovedSnapshot;
            
            /* verify next block */
            var error = Verify(block);
            if (error != OperatingError.Ok)
                return error;
            
            /* check next block index */
            var currentBlockHeader = _stateManager.LastApprovedSnapshot.Blocks.GetTotalBlockHeight();
            if (!_IsGenesisBlock(block) && currentBlockHeader + 1 != block.Header.Index)
                return OperatingError.InvalidNonce;
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
            
            /* check do we have all transcations specified */
            foreach (var txHash in block.TransactionHashes)
            {
                if (!currentTransactions.ContainsKey(txHash))
                    return OperatingError.TransactionLost;
            }
            
            /* confirm block transactions */
            var validatorAddress = _crypto.ComputeAddress(block.Header.Validator.Buffer.ToByteArray()).ToUInt160();
            
            /* execute transactions */
            foreach (var txHash in block.TransactionHashes)
            {
                /* try to find transaction by hash */
                var transaction = currentTransactions[txHash];
                transaction.GasUsed = 21_000;
                var snapshot = _stateManager.NewSnapshot();
                
                /* try to execute transaction */
                var result = _transactionManager.Execute(block, transaction, snapshot);
                if (result != OperatingError.Ok)
                {
                    _stateManager.Rollback();
                    snapshot = _stateManager.NewSnapshot();
                    if (_TakeTransactionFee(validatorAddress, transaction, snapshot) != OperatingError.Ok)
                        throw new Exception($"Unable to take fee for transaction {transaction.Hash.Buffer.ToHex()}");
                    snapshot.Transactions.AddTransaction(transaction, TransactionStatus.Failed);
                    _logger.LogWarning($"Unable to execute transaction {txHash.Buffer.ToHex()} with nonce ({transaction.Transaction?.Nonce}), {result}");
                    _stateManager.Approve();
                    continue;
                }
                
                /* try to take fee from sender */
                result = _TakeTransactionFee(validatorAddress, transaction, snapshot);
                if (result != OperatingError.Ok)
                {
                    _stateManager.Rollback();
                    _logger.LogWarning($"Unable to execute transaction {txHash.Buffer.ToHex()} with nonce ({transaction.Transaction?.Nonce}), {result}");
                    continue;
                }
                
                /* mark transaction as executed */
                if (_logger.IsEnabled(LogLevel.Debug))
                    _logger.LogDebug($"Successfully executed transaction {txHash.Buffer.ToHex()} with nonce ({transaction.Transaction.Nonce})");
                snapshot.Transactions.AddTransaction(transaction, TransactionStatus.Executed);
                _stateManager.Approve();
            }
            block.GasPrice = _CalcEstimatedBlockFee(block.TransactionHashes);
            
            /* save block to repository */
            var snapshotBlock = _stateManager.NewSnapshot();
            snapshotBlock.Blocks.AddBlock(block);
            _stateManager.Approve();

            if (checkStateHash && !_stateManager.LastApprovedSnapshot.StateHash.Equals(block.Header.StateHash))
            {
                _stateManager.RollbackTo(snapshotBefore);
                return OperatingError.InvalidState;
            }

            if (commit)
            {
                _logger.LogInformation($"Persisted new block {block.Header.Index} with hash {block.Hash} and txs {block.TransactionHashes.Count} in {TimeUtils.CurrentTimeMillis() - startTime} ms");
                /* flush changes to database */
                _stateManager.Commit();
                OnBlockPersisted?.Invoke(this, block);
            }
            return OperatingError.Ok;
        }

        private OperatingError _TakeTransactionFee(UInt160 validatorAddress, TransactionReceipt transaction, IBlockchainSnapshot snapshot)
        {
            /* genesis block doesn't have LA asset and validators are fee free */
            if (_validatorManager.CheckValidator(transaction.Transaction.From))
                return OperatingError.Ok;
            /* check availabe LA balance */
            var fee = new Money(transaction.GasUsed * transaction.Transaction.GasPrice * (ulong) 1e9);
            /* transfer fee from wallet to validator */
            return snapshot.Balances.TransferBalance(transaction.Transaction.From, validatorAddress, fee)
                ? OperatingError.Ok
                : OperatingError.InsufficientBalance;
        }
        
        public Signature Sign(BlockHeader block, KeyPair keyPair)
        {
            return _crypto.Sign(block.ToHash256().Buffer.ToByteArray(), keyPair.PrivateKey.Buffer.ToByteArray())
                .ToSignature();
        }

        public OperatingError VerifySignature(BlockHeader blockHeader, Signature signature, PublicKey publicKey)
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
            if (header.Timestamp == 0)
                return OperatingError.InvalidBlock;
            if (header.Validator is null)
                return OperatingError.InvalidBlock;
            return OperatingError.Ok;
        }

        private ulong _CalcEstimatedBlockFee(IEnumerable<UInt256> txHashes)
        {
            var arrayOfHashes = txHashes as UInt256[] ?? txHashes.ToArray();
            if (arrayOfHashes.Length == 0)
                return 0;
            var sum = 0UL;
            foreach (var txHash in arrayOfHashes)
            {
                var tx = _transactionManager.GetByHash(txHash);
                if (tx is null)
                {
                    _logger.LogWarning($"Unable to calculate fee, transaction lost ({txHash})");
                    return 0;
                }
                sum += tx.GasUsed * tx.Transaction.GasPrice;
            }
            return sum / (ulong) arrayOfHashes.Length;
        }
        
        public ulong CalcEstimatedFee(UInt256 blockHash)
        {
            var block = _stateManager.LastApprovedSnapshot.Blocks.GetBlockByHash(blockHash);
            return block.GasPrice != 0 ? block.GasPrice : _CalcEstimatedBlockFee(block.TransactionHashes);
        }

        public ulong CalcEstimatedFee()
        {
            var currentHeight = _stateManager.LastApprovedSnapshot.Blocks.GetTotalBlockHeight();
            var block = _stateManager.LastApprovedSnapshot.Blocks.GetBlockByHeight(currentHeight);
            return block.GasPrice != 0 ? block.GasPrice : _CalcEstimatedBlockFee(block.TransactionHashes);
        }
    }
}