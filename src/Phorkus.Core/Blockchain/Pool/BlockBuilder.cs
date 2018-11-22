using System;
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
            var header = new BlockHeader
            {
                Version = 0,
                PrevBlockHash = _prevBlockHash,
                MerkleRoot = MerkleTree.ComputeRoot(_transactions.Select(tx => tx.Hash).ToArray()),
                Timestamp = TimeUtils.CurrentTimeMillis(),
                Index = _prevBlockIndex + 1,
                Nonce = nonce
            };
            header.TransactionHashes.AddRange(_transactions.Select(tx => tx.Hash));
            var block = new Block
            {
                Hash = header.ToByteArray().ToHash256(),
                Header = header
            };
            return new BlockWithTransactions(block, _transactions);
        }
    }
}