using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using Phorkus.Core.Blockchain.Genesis;
using Phorkus.Proto;
using Phorkus.Core.Utils;
using Phorkus.Crypto;
using Phorkus.Storage.Repositories;
using Phorkus.Storage.State;
using Phorkus.Utility;
using Phorkus.Utility.Utils;

namespace Phorkus.Core.Blockchain.OperationManager.BlockManager
{
    public class BlockManager : IBlockManager
    {
        private readonly IGlobalRepository _globalRepository;
        private readonly IBlockRepository _blockRepository;
        private readonly ITransactionManager _transactionManager;
        private readonly ICrypto _crypto;
        private readonly IValidatorManager _validatorManager;
        private readonly IGenesisBuilder _genesisBuilder;
        private readonly IMultisigVerifier _multisigVerifier;
        private readonly Logger.ILogger<IBlockManager> _logger;
        private readonly IStateManager _stateManager;

        public BlockManager(
            IGlobalRepository globalRepository,
            IBlockRepository blockRepository,
            ITransactionManager transactionManager,
            ICrypto crypto,
            IValidatorManager validatorManager,
            IGenesisBuilder genesisBuilder,
            IMultisigVerifier multisigVerifier, Logger.ILogger<IBlockManager> logger,
            IStateManager stateManager)
        {
            _globalRepository = globalRepository;
            _blockRepository = blockRepository;
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
            return _blockRepository.GetBlockByHeight(blockHeight);
        }
        
        public Block GetByHash(UInt256 blockHash)
        {
            return _blockRepository.GetBlockByHash(blockHash);
        }

        private bool _IsGenesisBlock(Block block)
        {
            return block.Hash.Equals(_genesisBuilder.Build().Block.Hash);
        }

        private void _TryFixGlobalConfig(ulong blockHeight)
        {
            /* TODO: "fix me in future by using new state manager" */
            if (blockHeight == 0)
                return;
            while (true)
            {
                var block = _blockRepository.GetBlockByHeight(blockHeight);
                if (block is null)
                    break;
                ++blockHeight;
            }
            _globalRepository.SetTotalBlockHeight(blockHeight - 1);
        }
        
        public OperatingError Persist(Block block)
        {
            var startTime = TimeUtils.CurrentTimeMillis();
            /* verify next block */
            var error = Verify(block);
            if (error != OperatingError.Ok)
                return error;
            /* check next block index */
            var currentBlockHeader = _globalRepository.GetTotalBlockHeight();
            if (!_IsGenesisBlock(block) && currentBlockHeader + 1 != block.Header.Index)
                return OperatingError.InvalidNonce;
            var exists = _blockRepository.GetBlockByHeight(block.Header.Index);
            if (exists != null)
            {
                _TryFixGlobalConfig(block.Header.Index);
                return OperatingError.BlockAlreadyExists;
            }
            /* check prev block hash */
            var latestBlock = _blockRepository.GetBlockByHeight(currentBlockHeader);
            if (latestBlock != null && !block.Header.PrevBlockHash.Equals(latestBlock.Hash))
                return OperatingError.PrevBlockHashMismatched;
            /* verify block signatures */
            error = VerifySignatures(block);
            if (error != OperatingError.Ok)
                return error;
            /* confirm block transactions */
            foreach (var txHash in block.TransactionHashes)
            {
                if (_transactionManager.GetByHash(txHash) is null)
                    return OperatingError.TransactionLost;
            }
            var validatorAddress = _crypto.ComputeAddress(block.Header.Validator.Buffer.ToByteArray()).ToUInt160();
            /* execute transactions */
            foreach (var txHash in block.TransactionHashes)
            {
                var tx = _transactionManager.GetByHash(txHash);
                /* TODO: "change fee calculation logic" */
                var feeSnapshot = _stateManager.NewSnapshot();
                var asset = feeSnapshot.Assets.GetAssetByName("LA");
                if (asset != null && block.Header.Index > 0)
                    feeSnapshot.Balances.TransferAvailableBalance(tx.Transaction.From, validatorAddress, asset.Hash, tx.Transaction.Fee.ToMoney());
                _stateManager.Approve();
                /* execute transaction */
                var snapshot = _stateManager.NewSnapshot();
                var result = _transactionManager.Execute(block, txHash, snapshot);
                if (result == OperatingError.Ok)
                {
                    _stateManager.Approve();
                    if (_logger.IsEnabled(LogLevel.Debug))
                        _logger.LogDebug($"Successfully executed transaction {txHash.Buffer.ToHex()} with nonce ({_transactionManager.GetByHash(txHash).Transaction.Nonce})");
                    continue;
                }
                _logger.LogWarning($"Unable to execute transaction {txHash.Buffer.ToHex()} with nonce ({tx?.Transaction?.Nonce}, excepted {_transactionManager.CalcNextTxNonce(tx?.Transaction?.From)}), {result}");
                _stateManager.Rollback();                
            }
            block.AverageFee = _CalcEstimatedBlockFee(block.TransactionHashes).ToUInt256();
            /* write block to database */
            _blockRepository.AddBlock(block);
            _stateManager.Commit();
            var currentHeaderHeight = _globalRepository.GetTotalBlockHeight();
            if (block.Header.Index > currentHeaderHeight)
                _globalRepository.SetTotalBlockHeaderHeight(block.Header.Index);
            _globalRepository.SetTotalBlockHeight(block.Header.Index);
            var elapsedTime = TimeUtils.CurrentTimeMillis() - startTime;
            _logger.LogInformation($"Persisted new block {block.Header.Index} with hash {block.Hash} and txs {block.TransactionHashes.Count} in {elapsedTime} ms");
            OnBlockPersisted?.Invoke(this, block);
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

        public Money CalcEstimatedFee(UInt256 blockHash)
        {
            var block = _blockRepository.GetBlockByHash(blockHash);
            return block.AverageFee != null ? block.AverageFee.ToMoney() : _CalcEstimatedBlockFee(block.TransactionHashes);
        }

        public Money CalcEstimatedFee()
        {
            var currentHeight = _globalRepository.GetTotalBlockHeight();
            var block = _blockRepository.GetBlockByHeight(currentHeight);
            return block.AverageFee != null ? block.AverageFee.ToMoney() : _CalcEstimatedBlockFee(block.TransactionHashes);
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
    }
}