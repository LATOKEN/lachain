using NeoSharp.Core.Models;

namespace NeoSharp.Core.Blockchain
{
    public interface IBlockchainContext
    {
        Block CurrentBlock { get; set; }

        BlockHeader LastBlockHeader { get; set; }

        bool IsSyncing { get; set; }
        
        bool NeedPeerSync(uint peerCurrentBlockIndex);
    }
}