using System;
using System.Linq;
using Phorkus.Core.Blockchain.Genesis;
using Phorkus.Core.Cryptography;
using Phorkus.Core.Logging;
using Phorkus.Proto;
using Phorkus.Core.Storage;
using Phorkus.Core.Utils;

namespace Phorkus.Core.Blockchain.OperationManager.BlockManager
{
    public class BlockManager : IBlockManager
    {
        private readonly IGlobalRepository _globalRepository;
        private readonly IBlockRepository _blockRepository;
        private readonly ITransactionManager _transactionManager;
        private readonly ICrypto _crypto;
        private readonly IGenesisBuilder _genesisBuilder;
        private readonly ILogger<IBlockManager> _logger;
        
        public BlockManager(
            IGlobalRepository globalRepository,
            IBlockRepository blockRepository,
            ITransactionManager transactionManager,
            ICrypto crypto,
            IGenesisBuilder genesisBuilder,
            ILogger<IBlockManager> logger)
        {
            _globalRepository = globalRepository;
            _blockRepository = blockRepository;
            _transactionManager = transactionManager;
            _crypto = crypto;
            _genesisBuilder = genesisBuilder;
            _logger = logger;
        }

        public event EventHandler<Block> OnBlockPersisted;
        public event EventHandler<Block> OnBlockSigned;

        public Block GetByHash(UInt256 blockHash)
        {
            return _blockRepository.GetBlockByHash(blockHash);
        }

        private bool _IsGenesisBlock(Block block)
        {
            return block.Hash.Equals(_genesisBuilder.Build().Block.Hash);
        }

        public OperatingError Persist(Block block)
        {
            /* verify next block */
            var error = Verify(block);
            if (error != OperatingError.Ok)
                return error;
            /* check next block index */
            var currentBlockHeader = _globalRepository.GetTotalBlockHeight();
            if (!_IsGenesisBlock(block) && currentBlockHeader + 1 != block.Header.Index)
                return OperatingError.SequenceMismatched;
            var exists = _blockRepository.GetBlockByHeight(block.Header.Index);
            if (exists != null)
                return OperatingError.BlockAlreadyExists;
            /* check prev block hash */
            var latestBlock = _blockRepository.GetBlockByHeight(currentBlockHeader);
            if (latestBlock != null && !block.Header.PrevBlockHash.Equals(latestBlock.Hash))
                return OperatingError.SequenceMismatched;
            /* verify block signatures */
            error = VerifySignatures(block);
            if (error != OperatingError.Ok)
                return error;
            /* confirm block transactions */
            foreach (var txHash in block.Header.TransactionHashes)
            {
                if (_transactionManager.GetByHash(txHash) is null)
                    return OperatingError.TransactionLost;
            }
            foreach (var txHash in block.Header.TransactionHashes)
            {
                var result = _transactionManager.Execute(txHash);
                if (result == OperatingError.Ok)
                    continue;
                /* TODO: "we need block synchronization on transaction lost for example" */
                _logger.LogWarning($"Unable to execute transaction {txHash.Buffer.ToHex()}, {result}");
                /* TODO: "mark transaction as failed to execute here or something else" */
            }
            /* write block to database */
            _blockRepository.AddBlock(block);
            _logger.LogInformation($"Persisted new block with hash {block.Hash}");
            var currentHeaderHeight = _globalRepository.GetTotalBlockHeaderHeight();
            if (block.Header.Index > currentHeaderHeight)
                _globalRepository.SetTotalBlockHeaderHeight(block.Header.Index);
            _globalRepository.SetTotalBlockHeight(block.Header.Index);
            /*logger.LogInformation($"Changed current block height to {block.Header.Index}");*/
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
            var multisig = block.Multisig;
            if (!block.Header.ToHash256().Equals(block.Hash))
                return OperatingError.HashMismatched;
            var hash = block.Hash;
            var verified = 0;
            if (_IsGenesisBlock(block))
                return OperatingError.Ok;
            if (multisig is null)
                return OperatingError.InvalidMultisig;
            if (multisig.Signatures.Select(sig => sig.Key).Distinct().Count() != multisig.Signatures.Count)
                return OperatingError.InvalidMultisig;
            if (multisig.Validators.Distinct().Count() != multisig.Validators.Count)
                return OperatingError.InvalidMultisig;
            foreach (var entry in multisig.Signatures)
            {
                if (!multisig.Validators.Contains(entry.Key))
                    continue;
                var publicKey = entry.Key.Buffer.ToByteArray();
                var sig = entry.Value.Buffer.ToByteArray();
                try
                {
                    if (!_crypto.VerifySignature(hash.Buffer.ToByteArray(), sig, publicKey))
                        continue;
                    ++verified;
                }
                catch (Exception)
                {
                    // ignore
                }
            }

            return verified >= multisig.Quorum ? OperatingError.Ok : OperatingError.QuorumNotReached;
        }

        public OperatingError Verify(Block block)
        {
            var header = block.Header;
            if (header.Version != 0)
                return OperatingError.InvalidBlock;
            if (block.Header.Index != 0 && header.PrevBlockHash.IsZero())
                return OperatingError.InvalidBlock;
            if (header.MerkleRoot is null || header.MerkleRoot.IsZero())
                return OperatingError.InvalidBlock;
            /* TODO: "verify merkle root here" */
            if (header.Timestamp == 0)
                return OperatingError.InvalidBlock;
            return OperatingError.Ok;
        }
    }
}