using System.Collections.Generic;
using System.Linq;
using Google.Protobuf;
using Phorkus.Core.Cryptography;
using Phorkus.Proto;
using Phorkus.Core.Utils;

namespace Phorkus.Core.Blockchain.Pool
{
    public class BlockBuilder
    {
        private readonly UInt256 _prevBlockHash;
        private readonly ulong _prevBlockIndex;
        private readonly IReadOnlyCollection<SignedTransaction> _transactions;

        public BlockBuilder(ITransactionPool transactionPool, UInt256 prevBlockHash, ulong prevBlockIndex)
        {
            _prevBlockHash = prevBlockHash;
            _prevBlockIndex = prevBlockIndex;
            _transactions = transactionPool.Peek();
        }
        
        public BlockBuilder(IReadOnlyCollection<SignedTransaction> transactions, UInt256 prevBlockHash, ulong prevBlockIndex)
        {
            _prevBlockHash = prevBlockHash;
            _prevBlockIndex = prevBlockIndex;
            _transactions = transactions;
        }

        public BlockWithTransactions Build(ulong nonce)
        {
            return Build(null, nonce);
        }
        
        public BlockWithTransactions Build(SignedTransaction minerTransaction, ulong nonce)
        {
            var txs = _transactions;
            if (minerTransaction != null)
                txs = new[] {minerTransaction}.Concat(txs).ToArray();
            var merkeRoot = UInt256Utils.Zero;
            if (txs.Count > 0)
                merkeRoot = MerkleTree.ComputeRoot(txs.Select(tx => tx.Hash).ToArray());
            var header = new BlockHeader
            {
                Version = 0,
                PrevBlockHash = _prevBlockHash,
                MerkleRoot = merkeRoot,
                Timestamp = TimeUtils.CurrentTimeMillis() / 1000,
                Index = _prevBlockIndex + 1,
                Nonce = nonce
            };
            var block = new Block
            {
                Hash = header.ToByteArray().ToHash256(),
                TransactionHashes = { txs.Select(tx => tx.Hash) },
                Header = header
            };
            return new BlockWithTransactions(block, txs);
        }
    }
}