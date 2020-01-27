using System.Collections.Generic;
using System.Linq;
using Google.Protobuf;
using Phorkus.Core.Utils;
using Phorkus.Crypto;
using Phorkus.Proto;
using Phorkus.Utility.Utils;

namespace Phorkus.Core.Blockchain
{
    public class BlockBuilder
    {
        private readonly BlockHeader _prevBlock;
        private readonly UInt256 _stateHash;
        private readonly ECDSAPublicKey _validator;
        
        private ICollection<TransactionReceipt> _transactions;
        private MultiSig _multiSig;

        public BlockBuilder(BlockHeader prevBlock, ECDSAPublicKey validator, UInt256 stateHash = null)
        {
            _prevBlock = prevBlock;
            _stateHash = stateHash ?? UInt256Utils.Zero;
            _validator = validator;
        }

        public BlockBuilder WithTransactions(IReadOnlyCollection<TransactionReceipt> transactions)
        {
            _transactions = new List<TransactionReceipt>(transactions);
            return this;
        }

        public BlockBuilder WithTransactions(ICollection<TransactionReceipt> transactions)
        {
            _transactions = transactions;
            return this;
        }

        public BlockBuilder WithTransactions(ITransactionPool transactionPool)
        {
            _transactions = new List<TransactionReceipt>(transactionPool.Peek());
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
            var txs = _transactions;
            var merkeRoot = UInt256Utils.Zero;
            if (txs.Count > 0)
                merkeRoot = MerkleTree.ComputeRoot(txs.Select(tx => tx.Hash).ToArray());
            var header = new BlockHeader
            {
                PrevBlockHash = _prevBlock != null ? _prevBlock.ToHash256() : UInt256Utils.Zero,
                MerkleRoot = merkeRoot,
                Timestamp = TimeUtils.CurrentTimeMillis() / 1000,
                Index = _prevBlock?.Index + 1 ?? 0,
                Validator = _validator,
                StateHash = _stateHash,
                Nonce = nonce
            };
            var block = new Block
            {
                Hash = header.ToByteArray().ToHash256(),
                TransactionHashes = { txs.Select(tx => tx.Hash) },
                Header = header,
                Multisig = new MultiSig()
            };
            return new BlockWithTransactions(block, txs);
        }
    }
}