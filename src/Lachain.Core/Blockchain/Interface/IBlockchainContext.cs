using Lachain.Proto;

namespace Lachain.Core.Blockchain.Interface
{
    public interface IBlockchainContext
    {
        ulong CurrentBlockHeight { get; }

        Block? CurrentBlock { get; }
    }
}