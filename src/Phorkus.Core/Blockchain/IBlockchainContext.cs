using Phorkus.Proto;

namespace Phorkus.Core.Blockchain
{
    public interface IBlockchainContext
    {
        ulong CurrentBlockHeight { get; }

        Block CurrentBlock { get; }
    }
}