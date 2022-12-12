using Lachain.Proto;
using Lachain.Utility;

namespace Lachain.Networking
{
    public interface IConsensusMessageDeliverer
    {
        void SendTo(ECDSAPublicKey publicKey, NetworkMessage networkMessage, NetworkMessagePriority priority);
        void IncPenalty(ECDSAPublicKey publicKey);
    }
}