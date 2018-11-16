namespace Phorkus.Core.Blockchain
{
    public interface IBlockchainContext
    {
        ulong CurrentBlockHeaderHeight { get; }
        
        ulong CurrentBlockHeight { get; }
        
        HashedBlockHeader CurrentBlockHeader { get; }

        HashedBlockHeader CurrentBlock { get; }
    }
}