using System;
using System.Linq;
using NeoSharp.Core.Cryptography;
using NeoSharp.Core.Extensions;
using NeoSharp.Core.Models;
using NeoSharp.Core.Models.OperationManager;
using NeoSharp.Core.Models.Transactions;
using NeoSharp.Types;

namespace NeoSharp.Core.Blockchain
{
    class BlockProducer : IBlockProducer
    {
        private readonly IBlockchainContext _blockchainContext;
        private readonly ITransactionPool _transactionPool;
        private readonly ISigner<Transaction> _transactionSigner;

        public BlockProducer(
            IBlockchainContext blockchainContext,
            ITransactionPool transactionPool,
            ISigner<Transaction> transactionSigner)
        {
            _blockchainContext = blockchainContext;
            _transactionPool = transactionPool;
            _transactionSigner = transactionSigner;
            Version = 0;
        }

        public uint Version { get; }

        public Block ProduceBlock(int maxSize, DateTime generationTime, ulong nonce, UInt160 producerAddress)
        {
            var currentBlock = _blockchainContext.CurrentBlock;
            var generationTimestamp = Math.Max(generationTime.ToTimestamp(), currentBlock.Timestamp + 1);
            var minerTransaction = new MinerTransaction
            {
                Nonce = (uint) (nonce % (uint.MaxValue + 1ul))
            };
            minerTransaction.From = producerAddress;
            _transactionSigner.Sign(minerTransaction);
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