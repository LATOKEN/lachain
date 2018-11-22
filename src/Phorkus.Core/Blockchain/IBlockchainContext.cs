using Phorkus.Proto;

namespace Phorkus.Core.Blockchain
{
    public interface IBlockchainContext
    {
        ulong CurrentBlockHeaderHeight { get; }
        
        ulong CurrentBlockHeight { get; }
        
        Block CurrentBlockHeader { get; }

        Block CurrentBlock { get; }
    }
}