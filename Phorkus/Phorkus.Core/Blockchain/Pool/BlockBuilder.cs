using System.Collections.Generic;
using System.Linq;
using Google.Protobuf;
using Phorkus.Core.Proto;
using Phorkus.Core.Utils;

namespace Phorkus.Core.Blockchain.Pool
{
    public class BlockBuilder
    {
        private readonly IReadOnlyCollection<SignedTransaction> _hashedTransactions;
        private readonly Block _prevBlock;

        public BlockBuilder(ITransactionPool transactionPool, Block prevBlock)
        {
            _hashedTransactions = transactionPool.Peek();
            _prevBlock = prevBlock;
        }
        
        public BlockBuilder(IReadOnlyCollection<SignedTransaction> hashedTransactions, Block prevBlock)
        {
            _hashedTransactions = hashedTransactions;
            _prevBlock = prevBlock;
        }
        
        public BlockWithTransactions Build(ulong nonce)
        {
            var header = new BlockHeader
            {
                Version = 1,
                PrevBlockHash = _prevBlock.Hash,
                MerkleRoot = null,
                Timestamp = TimeUtils.CurrentTimeMillis(),
                Index = _prevBlock.Header.Index + 1,
                Type = HeaderType.Extended,
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