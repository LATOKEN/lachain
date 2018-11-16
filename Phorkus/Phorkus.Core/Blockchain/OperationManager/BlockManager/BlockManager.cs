using System;
using Phorkus.Core.Cryptography;
using Phorkus.Core.Proto;
using Phorkus.Core.Storage;

namespace Phorkus.Core.Blockchain.OperationManager.BlockManager
{
    public class BlockManager : IBlockManager
    {
        private readonly IBlockRepository _blockRepository;
        
        public BlockManager(IBlockRepository blockRepository)
        {
            _blockRepository = blockRepository;
        }
        
        public event EventHandler<Block> OnBlockPersisted;
        public event EventHandler<Block> OnBlockSigned;
        
        public Block GetByHash(UInt256 blockHash)
        {
            throw new NotImplementedException();
        }

        public void Persist(Block transaction)
        {
            throw new NotImplementedException();
        }

        public Block Sign(Block blockHeader, KeyPair keyPair)
        {
            throw new NotImplementedException();
        }

        public OperatingError Verify(Block blockHeader)
        {
            throw new NotImplementedException();
        }
    }
}