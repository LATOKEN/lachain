using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using Phorkus.Core.Blockchain.Genesis;
using Phorkus.Proto;
using Phorkus.Core.Utils;
using Phorkus.Crypto;
using Phorkus.Storage.State;
using Phorkus.Utility;
using Phorkus.Utility.Utils;

namespace Phorkus.Core.Blockchain.OperationManager.BlockManager
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
        
        public OperatingError Execute(Block block, IEnumerable<AcceptedTransaction> transactions)
        {
            var currentTransactions = transactions.ToDictionary(tx => tx.Hash, tx => tx);
            var startTime = TimeUtils.CurrentTimeMillis();
            
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
                var snapshot = _stateManager.NewSnapshot();
                
                /* try to take fee from sender */
                var result = _TakeTransactionFee(validatorAddress, transaction, snapshot);
                if (result != OperatingError.Ok)
                {
                    _stateManager.Rollback();
                    _logger.LogWarning($"Unable to execute transaction {txHash.Buffer.ToHex()} with nonce ({transaction.Transaction?.Nonce}, excepted {_transactionManager.CalcNextTxNonce(transaction.Transaction?.From)}), {result}");
                    continue;
                }
                
                /* try to execute transaction */
                result = _transactionManager.Execute(block, transaction, snapshot);
                if (result != OperatingError.Ok)
                {
                    _stateManager.Rollback();
                    snapshot = _stateManager.NewSnapshot();
                    if (_TakeTransactionFee(validatorAddress, transaction, snapshot) != OperatingError.Ok)
                        throw new Exception($"Unable to take fee for transaction {transaction.Hash.Buffer.ToHex()}");
                    snapshot.Transactions.AddTransaction(transaction, TransactionStatus.Failed);
                    _logger.LogWarning($"Unable to execute transaction {txHash.Buffer.ToHex()} with nonce ({transaction.Transaction?.Nonce}, excepted {_transactionManager.CalcNextTxNonce(transaction.Transaction?.From)}), {result}");
                    _stateManager.Approve();
                    continue;
                }
                
                /* mark transaction as executed */
                if (_logger.IsEnabled(LogLevel.Debug))
                    _logger.LogDebug($"Successfully executed transaction {txHash.Buffer.ToHex()} with nonce ({transaction.Transaction.Nonce})");
                snapshot.Transactions.AddTransaction(transaction, TransactionStatus.Executed);
                _stateManager.Approve();
            }
            block.AverageFee = _CalcEstimatedBlockFee(block.TransactionHashes).ToUInt256();
            
            /* save block to repository */
            var snapshotBlock = _stateManager.NewSnapshot();
            snapshotBlock.Blocks.AddBlock(block);
            _logger.LogInformation($"Persisted new block {block.Header.Index} with hash {block.Hash} and txs {block.TransactionHashes.Count} in {TimeUtils.CurrentTimeMillis() - startTime} ms");
            _stateManager.Approve();
            
            /* flush changes to database */
            _stateManager.Commit();
            
            OnBlockPersisted?.Invoke(this, block);
            return OperatingError.Ok;
        }

        private OperatingError _TakeTransactionFee(UInt160 validatorAddress, AcceptedTransaction transaction, IBlockchainSnapshot snapshot)
        {
            var asset = snapshot.Assets.GetAssetByName("LA");
            if (asset is null)
                return OperatingError.Ok;
            /* genesis block doesn't have LA asset and validators fee free */
            if (_validatorManager.CheckValidator(transaction.Transaction.From))
                return OperatingError.Ok;
            /* check availabe LA balance */
            var availableBalance = snapshot.Balances.GetAvailableBalance(transaction.Transaction.From, asset.Hash);
            if (availableBalance.CompareTo(transaction.Transaction.Fee.ToMoney()) < 0)
                return OperatingError.InsufficientBalance;
            /* transfer fee from wallet to validator */
            snapshot.Balances.TransferAvailableBalance(transaction.Transaction.From, validatorAddress,
                asset.Hash, transaction.Transaction.Fee.ToMoney());
            return OperatingError.Ok;
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
            if (header.Version != 0)
                return OperatingError.InvalidBlock;
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

        private Money _CalcEstimatedBlockFee(IEnumerable<UInt256> txHashes)
        {
            var arrayOfHashes = txHashes as UInt256[] ?? txHashes.ToArray();
            if (arrayOfHashes.Length == 0)
                return Money.Zero;
            var sum = Money.Zero;
            foreach (var txHash in arrayOfHashes)
            {
                var tx = _transactionManager.GetByHash(txHash);
                if (tx is null)
                {
                    _logger.LogWarning($"Unable to calculate fee, transaction lost ({txHash})");
                    return Money.Zero;
                }
                sum += tx.Transaction.Fee.ToMoney();
            }
            return sum / arrayOfHashes.Length;
        }
        
        public Money CalcEstimatedFee(UInt256 blockHash)
        {
            var block = _stateManager.LastApprovedSnapshot.Blocks.GetBlockByHash(blockHash);
            return block.AverageFee != null ? block.AverageFee.ToMoney() : _CalcEstimatedBlockFee(block.TransactionHashes);
        }

        public Money CalcEstimatedFee()
        {
            var currentHeight = _stateManager.LastApprovedSnapshot.Blocks.GetTotalBlockHeight();
            var block = _stateManager.LastApprovedSnapshot.Blocks.GetBlockByHeight(currentHeight);
            return block.AverageFee != null ? block.AverageFee.ToMoney() : _CalcEstimatedBlockFee(block.TransactionHashes);
        }
    }
}