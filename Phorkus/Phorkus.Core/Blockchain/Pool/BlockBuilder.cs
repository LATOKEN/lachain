using System;
using System.Collections.Generic;
using System.Linq;
using Google.Protobuf;
using Phorkus.Core.Proto;
using Phorkus.Core.Utils;

namespace Phorkus.Core.Blockchain.Pool
{
    public class BlockBuilder
    {
        private readonly IReadOnlyCollection<HashedTransaction> _hashedTransactions;
        private readonly HashedBlockHeader _prevBlockHeader;

        public BlockBuilder(ITransactionPool transactionPool, IBlockchainContext blockchainContext)
        {
            _prevBlockHeader = blockchainContext.CurrentBlockHeader ?? throw new ArgumentNullException(nameof(blockchainContext.CurrentBlockHeader));
            _hashedTransactions = transactionPool.Peek();
        }
        
        public BlockBuilder(IReadOnlyCollection<HashedTransaction> hashedTransactions, HashedBlockHeader prevBlockHeader)
        {
            _hashedTransactions = hashedTransactions;
            _prevBlockHeader = prevBlockHeader;
        }
        
        public Block Build(ulong nonce)
        {
            var header = new BlockHeader
            {
                Version = 1,
                PrevBlockHash = _prevBlockHeader.Hash,
                MerkleRoot = null,
                Timestamp = TimeUtils.CurrentTimeMillis(),
                Index = _prevBlockHeader.BlockHeader.Index + 1,
                Type = HeaderType.Extended,
                Nonce = nonce,
                Multisig = new MultiSig()
            };
            header.TransactionHashes.AddRange(_hashedTransactions.Select(tx => tx.Hash));
            var block = new Block
            {
                Hash = header.ToByteArray().ToHash256(),
                Header = header
            };
            block.Transactions.AddRange(_hashedTransactions.Select(tx => tx.Transaction));
            return block;
            
        }
    }
}