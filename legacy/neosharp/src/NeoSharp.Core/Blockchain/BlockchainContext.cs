using NeoSharp.Core.Models;

namespace NeoSharp.Core.Blockchain
{
    public class BlockchainContext : IBlockchainContext
    {
        public BlockHeader CurrentBlock { get; set; }

        public BlockHeader LastBlockHeader { get; set; }

        public bool IsSyncing { get; set; }

        bool IBlockchainContext.NeedPeerSync(uint peerCurrentBlockIndex)
        {
            if (LastBlockHeader is null)
                return false;
            return LastBlockHeader.Index < peerCurrentBlockIndex;
        }
    }
}