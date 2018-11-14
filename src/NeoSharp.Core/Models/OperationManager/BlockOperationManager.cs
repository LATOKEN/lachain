using System.Linq;
using NeoSharp.BinarySerialization;
using NeoSharp.Core.Cryptography;
using NeoSharp.Core.Storage.Blockchain;
using NeoSharp.Cryptography;

namespace NeoSharp.Core.Models.OperationManager
{
    public class BlockOperationManager : IBlockOperationsManager
    {
        #region Private Fields

        private readonly Crypto _crypto;
        private readonly IBinarySerializer _binarySerializer;
        private readonly ISigner<Transaction> _transactionSigner;
        private readonly IBlockRepository _blockRepository;

        #endregion

        #region Constructor 

        public BlockOperationManager(
            Crypto crypto,
            IBinarySerializer binarySerializer,
            ISigner<Transaction> transactionSigner,
            IBlockRepository blockRepository)
        {
            _crypto = crypto;
            _binarySerializer = binarySerializer;
            _transactionSigner = transactionSigner;
            _blockRepository = blockRepository;
        }

        #endregion

        #region IBlockOperationsManager implementation 

        public void Sign(Block block)
        {
            // Compute tx hashes
            var txSize = block.Transactions?.Length ?? 0;
            block.TransactionHashes = new UInt256[txSize];

            for (var x = 0; x < txSize; x++)
            {
                _transactionSigner.Sign(block.Transactions?[x]);
                block.TransactionHashes[x] = block.Transactions?[x].Hash;
            }

            block.MerkleRoot = MerkleTree.ComputeRoot(block.TransactionHashes.ToArray());

            // Compute hash
            var serializedBlock = _binarySerializer.Serialize(block, new BinarySerializerSettings
            {
                Filter = a => a != nameof(block.MultiSig) &&
                              a != nameof(block.Transactions) &&
                              a != nameof(block.TransactionHashes) &&
                              a != nameof(block.Type)
            });

            block.Hash = new UInt256(_crypto.Hash256(serializedBlock));

            /* TODO: "sign multisig here" */
        }

        public bool Verify(Block block)
        {
            var prevHeader = _blockRepository.GetBlockHeaderByHash(block.PreviousBlockHash);
            if (prevHeader == null)
                return false;

            if (prevHeader.Index + 1 != block.Index)
            {
                return false;
            }

            if (prevHeader.Timestamp >= block.Timestamp)
            {
                return false;
            }

            /* TODO: "verify multisig here" */
            /*if (!_witnessOperationsManager.Verify(block.MultiSig))
            {
                return false;
            }*/

            return true;
        }

        #endregion
    }
}