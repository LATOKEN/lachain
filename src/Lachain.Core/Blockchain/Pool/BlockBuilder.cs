using System;
using System.Collections.Generic;
using System.Linq;
using Lachain.Core.Blockchain.Interface;
using Lachain.Crypto;
using Lachain.Crypto.Misc;
using Lachain.Proto;
using Lachain.Utility.Utils;

namespace Lachain.Core.Blockchain.Pool
{
    public class BlockBuilder
    {
        private readonly BlockHeader _prevBlock;
        private readonly UInt256 _stateHash;

        private ICollection<TransactionReceipt>? _transactions;
        private MultiSig? _multiSig;

        public BlockBuilder(BlockHeader prevBlock, UInt256? stateHash = null)
        {
            _prevBlock = prevBlock;
            _stateHash = stateHash ?? UInt256Utils.Zero;
        }

        public BlockBuilder WithTransactions(IEnumerable<TransactionReceipt> transactions)
        {
            _transactions = new List<TransactionReceipt>(transactions);
            return this;
        }

        public BlockBuilder WithMultisig(IEnumerable<ECDSAPublicKey> validators)
        {
            if (_multiSig is null)
                _multiSig = new MultiSig();
            var publicKeys = validators as ECDSAPublicKey[] ?? validators.ToArray();
            _multiSig.Validators.AddRange(publicKeys);
            _multiSig.Quorum = (uint) publicKeys.Length;
            return this;
        }

        public BlockBuilder WithMultisig(MultiSig multiSig)
        {
            _multiSig = multiSig;
            return this;
        }

        public BlockBuilder WithSignature(ECDSAPublicKey publicKey, Signature signature)
        {
            if (_multiSig is null)
                _multiSig = new MultiSig();
            _multiSig.Signatures.Add(new MultiSig.Types.SignatureByValidator
            {
                Key = publicKey,
                Value = signature
            });
            if (!_multiSig.Validators.Contains(publicKey))
                _multiSig.Validators.Add(publicKey);
            return this;
        }

        public BlockWithTransactions Build(ulong nonce)
        {
            var txs = _transactions ?? throw new InvalidOperationException();
            var merkleRoot = UInt256Utils.Zero;
            if (txs.Count > 0)
                merkleRoot = MerkleTree.ComputeRoot(txs.Select(tx => tx.Hash).ToArray()) ??
                             throw new InvalidOperationException();
            var header = new BlockHeader
            {
                PrevBlockHash = _prevBlock != null ? _prevBlock.Keccak() : UInt256Utils.Zero,
                MerkleRoot = merkleRoot,
                Index = _prevBlock?.Index + 1 ?? 0,
                StateHash = _stateHash,
                Nonce = nonce
            };
            var block = new Block
            {
                Hash = header.Keccak(),
                TransactionHashes = {txs.Select(tx => tx.Hash)},
                Header = header,
                Multisig = _multiSig ?? new MultiSig(),
                Timestamp = TimeUtils.CurrentTimeMillis(),
            };
            return new BlockWithTransactions(block, txs);
        }
    }
}