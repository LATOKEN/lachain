using System.Collections.Generic;
using System.Linq;
using Google.Protobuf;
using Phorkus.Proto;
using Phorkus.Core.Utils;
using Phorkus.Crypto;
using Phorkus.Utility.Utils;

namespace Phorkus.Core.Blockchain.Pool
{
    public class BlockBuilder
    {
        private readonly BlockHeader _prevBlock;
        
        private IReadOnlyCollection<SignedTransaction> _transactions;
        private MultiSig _multiSig;
        private SignedTransaction _minerTransaction;

        public BlockBuilder(BlockHeader prevBlock)
        {
            _prevBlock = prevBlock;
        }

        public BlockBuilder WithTransactions(IReadOnlyCollection<SignedTransaction> transactions)
        {
            _transactions = transactions;
            return this;
        }

        public BlockBuilder WithTransactions(ITransactionPool transactionPool)
        {
            _transactions = transactionPool.Peek();
            return this;
        }

        public BlockBuilder WithMultisig(IEnumerable<PublicKey> validators)
        {
            if (_multiSig is null)
                _multiSig = new MultiSig();
            var publicKeys = validators as PublicKey[] ?? validators.ToArray();
            _multiSig.Validators.AddRange(publicKeys);
            _multiSig.Quorum = (uint) publicKeys.Length;
            return this;
        }
        
        public BlockBuilder WithSignature(PublicKey publicKey, Signature signature)
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

        public BlockBuilder WithMiner(SignedTransaction minerTransaction)
        {
            _minerTransaction = minerTransaction;
            return this;
        }
        
        public BlockWithTransactions Build(ulong nonce)
        {
            var txs = _transactions;
            if (_minerTransaction != null)
                txs = new[] {_minerTransaction}.Concat(txs).ToArray();
            var merkeRoot = UInt256Utils.Zero;
            if (txs.Count > 0)
                merkeRoot = MerkleTree.ComputeRoot(txs.Select(tx => tx.Hash).ToArray());
            var header = new BlockHeader
            {
                Version = 0,
                PrevBlockHash = _prevBlock != null ? _prevBlock.ToHash256() : UInt256Utils.Zero,
                MerkleRoot = merkeRoot,
                Timestamp = TimeUtils.CurrentTimeMillis() / 1000,
                Index = _prevBlock?.Index + 1 ?? 0,
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