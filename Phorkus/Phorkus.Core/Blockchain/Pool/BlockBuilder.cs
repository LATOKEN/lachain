using System.Collections.Generic;
using System.Linq;
using Google.Protobuf;
using Phorkus.Core.Proto;
using Phorkus.Core.Utils;

namespace Phorkus.Core.Blockchain.Pool
{
    public class BlockBuilder
    {
        private readonly UInt256 _prevBlockHash;
        private readonly ulong _prevBlockIndex;
        private readonly IReadOnlyCollection<SignedTransaction> _hashedTransactions;

        public BlockBuilder(ITransactionPool transactionPool, UInt256 prevBlockHash, ulong prevBlockIndex)
        {
            _prevBlockHash = prevBlockHash;
            _prevBlockIndex = prevBlockIndex;
            _hashedTransactions = transactionPool.Peek();
        }
        
        public BlockBuilder(IReadOnlyCollection<SignedTransaction> hashedTransactions, UInt256 prevBlockHash, ulong prevBlockIndex)
        {
            _prevBlockHash = prevBlockHash;
            _prevBlockIndex = prevBlockIndex;
            _hashedTransactions = hashedTransactions;
        }
        
        public BlockWithTransactions Build(ulong nonce)
        {
            var header = new BlockHeader
            {
                Version = 0,
                PrevBlockHash = _prevBlockHash,
                MerkleRoot = null,
                Timestamp = TimeUtils.CurrentTimeMillis(),
                Index = _prevBlockIndex + 1,
                Nonce = nonce
            };
            header.TransactionHashes.AddRange(_hashedTransactions.Select(tx => tx.Hash));
            var block = new Block
            {
                Hash = header.ToByteArray().ToHash256(),
                Header = header
            };
            return new BlockWithTransactions(block, _hashedTransactions);
        }
    }
}