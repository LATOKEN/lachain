using Lachain.Proto;

namespace Lachain.Networking.Consensus
{
    public interface IConsensusMessageDeliverer
    {
        void SendTo(ECDSAPublicKey publicKey, NetworkMessage networkMessage);
    }
}