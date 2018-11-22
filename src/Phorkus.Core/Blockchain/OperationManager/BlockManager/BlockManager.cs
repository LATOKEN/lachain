using System;
using Phorkus.Core.Blockchain.Genesis;
using Phorkus.Core.Cryptography;
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
        
        public BlockManager(
            IGlobalRepository globalRepository,
            IBlockRepository blockRepository,
            ITransactionManager transactionManager,
            ICrypto crypto,
            IGenesisBuilder genesisBuilder)
        {
            _globalRepository = globalRepository;
            _blockRepository = blockRepository;
            _transactionManager = transactionManager;
            _crypto = crypto;
            _genesisBuilder = genesisBuilder;
        }
        
        public event EventHandler<Block> OnBlockPersisted;
        public event EventHandler<Block> OnBlockSigned;
        
        public Block GetByHash(UInt256 blockHash)
        {
            return _blockRepository.GetBlockByHash(blockHash);
        }

        private bool _IsGenesisBlock(Block block)
        {
            /* TODO: "this code might cause null reference exception if genesis block not initialized on startup" */
            return block.Hash.Equals(_genesisBuilder.Build(null).Block.Hash);
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
                var result = _transactionManager.Execute(txHash);
                if (result == OperatingError.Ok)
                    continue;
                /* TODO: "mark transaction as failed here" */
            }
            /* write block to database */
            _blockRepository.AddBlock(block);
            var currentHeaderHeight = _globalRepository.GetTotalBlockHeaderHeight();
            if (block.Header.Index > currentHeaderHeight)
                _globalRepository.SetTotalBlockHeaderHeight(block.Header.Index);
            _globalRepository.SetTotalBlockHeight(block.Header.Index);
            return OperatingError.Ok;
        }
        
        public Signature Sign(BlockHeader block, KeyPair keyPair)
        {
            return _crypto.Sign(block.ToHash256().Buffer.ToByteArray(), keyPair.PrivateKey.Buffer.ToByteArray()).ToSignature();
        }
        
        public OperatingError VerifySignature(BlockHeader blockHeader, Signature signature, PublicKey publicKey)
        {
            var result = _crypto.VerifySignature(blockHeader.ToHash256().Buffer.ToByteArray(), signature.Buffer.ToByteArray(), publicKey.Buffer.ToByteArray());
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
            foreach (var entry in multisig.Signatures)
            {
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
            if (header.Nonce == 0)
                return OperatingError.InvalidNonce;
            return OperatingError.Ok;
        }
    }
}