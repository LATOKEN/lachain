using System;
using Phorkus.Core.Cryptography;
using Phorkus.Core.Proto;

namespace Phorkus.Core.Blockchain.OperationManager
{
    public interface IBlockManager
    {
        event EventHandler<Block> OnBlockPersisted;
        event EventHandler<Block> OnBlockSigned;
        
        Block GetByHash(UInt256 blockHash);
        
        void Persist(Block transaction);
        
        Signature Sign(BlockHeader blockHeader, KeyPair keyPair);
        
        OperatingError Verify(Block blockHeader);
    }
}