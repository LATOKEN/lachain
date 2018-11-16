using System;
using Phorkus.Core.Proto;

namespace Phorkus.Core.Blockchain.OperationManager
{
    public interface IBlockOperationManager
    {
        event EventHandler<Block> OnBlockPersisted;
        event EventHandler<Block> OnBlockSigned;

        bool Verify(BlockHeader blockHeader);
    }
}