using System;
using Phorkus.Proto;

namespace Phorkus.Core.Blockchain
{
    public interface IBlockchainContext
    {
        [Obsolete("legacy from NEO")]
        ulong CurrentBlockHeaderHeight { get; }
        
        ulong CurrentBlockHeight { get; }
        
        [Obsolete("legacy from NEO")]
        Block CurrentBlockHeader { get; }

        Block CurrentBlock { get; }
    }
}