using System;
using System.Linq;
using NeoSharp.Core.Cryptography;
using NeoSharp.Core.Extensions;
using NeoSharp.Core.Models;
using NeoSharp.Core.Models.Transactions;

namespace NeoSharp.Core.Blockchain
{
    class BlockProducer : IBlockProducer
    {
        private readonly IBlockchainContext _blockchainContext;
        private readonly ITransactionPool _transactionPool;

        public BlockProducer(IBlockchainContext blockchainContext, ITransactionPool transactionPool)
        {
            _blockchainContext = blockchainContext;
            _transactionPool = transactionPool;
            Version = 0;
        }

        public uint Version { get; }

        public Block ProduceBlock(int maxSize, DateTime generationTime, ulong nonce)
        {
            var currentBlock = _blockchainContext.CurrentBlock;
            var generationTimestamp = Math.Max(generationTime.ToTimestamp(), currentBlock.Timestamp + 1);
            MinerTransaction minerTransaction = new MinerTransaction
            {
                Nonce = (uint) (nonce % (uint.MaxValue + 1ul))
            };
            var transactions = new[] {minerTransaction}.Concat(_transactionPool.GetTransactions()).Take(maxSize)
                .ToArray();
            var transactionHashes = transactions.Select(tx => tx.Hash).ToArray();
            return new Block
            {
                Version = Version,
                PreviousBlockHash = currentBlock.Hash,
                MerkleRoot = MerkleTree.ComputeRoot(transactionHashes),
                Timestamp = generationTimestamp,
                Index = currentBlock.Index,
                Nonce = nonce,
                TransactionHashes = transactionHashes,
                Transactions = transactions
            };
        }
    }
}