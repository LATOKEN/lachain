using System;
using NeoSharp.BinarySerialization;
using NeoSharp.Core.Cryptography;
using NeoSharp.Cryptography;

namespace NeoSharp.Core.Models.OperationManager
{
    public class BlockHeaderOperationsManager : IBlockHeaderOperationsManager
    {
        #region Private Fields

        private readonly Crypto _crypto;
        private readonly IBinarySerializer _binarySerializer;

        #endregion

        #region Constructor 

        public BlockHeaderOperationsManager(
            Crypto crypto,
            IBinarySerializer binarySerializer)
        {
            _crypto = crypto;
            _binarySerializer = binarySerializer;
        }

        #endregion

        #region IBlockHeaderOperationsManager implementation 

        public void Sign(BlockHeader blockHeader)
        {
            // Check if the BlockHeader is already signed.
            if (blockHeader.Hash != null && blockHeader.Hash != UInt256.Zero) return;

            if (blockHeader.MerkleRoot == null)
            {
                // Compute hash
                blockHeader.MerkleRoot = MerkleTree.ComputeRoot(blockHeader.TransactionHashes);
            }

            var serializedBlockHeader = _binarySerializer.Serialize(blockHeader, new BinarySerializerSettings()
            {
                Filter = a => a != nameof(Type) &&
                              a != nameof(blockHeader.TransactionHashes)
            });

            blockHeader.Hash = new UInt256(_crypto.Hash256(serializedBlockHeader));
        }

        public bool Verify(BlockHeader blockHeader)
        {
            throw new NotImplementedException();
        }

        #endregion
    }
}