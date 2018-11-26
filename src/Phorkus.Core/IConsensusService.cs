using Phorkus.Core.Cryptography;
using Phorkus.Proto;

namespace Phorkus.Core
{
    public interface IConsensusService
    {
        BlockPrepareReply PrepareBlock(BlockPrepareRequest request, KeyPair keyPair);
        ChangeViewReply ChangeView(ChangeViewRequest request, KeyPair keyPair);
    }
}