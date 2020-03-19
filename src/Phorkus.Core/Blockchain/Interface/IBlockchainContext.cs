using Phorkus.Proto;

namespace Phorkus.Core.Blockchain.Interface
{
    public interface IBlockchainContext
    {
        ulong CurrentBlockHeight { get; }

        Block? CurrentBlock { get; }
    }
}