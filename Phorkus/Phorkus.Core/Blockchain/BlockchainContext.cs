using Phorkus.Core.Storage;

namespace Phorkus.Core.Blockchain
{
    public class BlockchainContext : IBlockchainContext
    {
        public ulong CurrentBlockHeaderHeight { get; internal set; }
        public ulong CurrentBlockHeight { get; internal set; }
        
        public HashedBlockHeader CurrentBlockHeader { get; internal set; }
        public HashedBlockHeader CurrentBlock { get; internal set; }

        private readonly IBlockRepository _blockRepository;
        
        public BlockchainContext(IBlockRepository blockRepository)
        {
            _blockRepository = blockRepository;
            
        }
        
    }
}