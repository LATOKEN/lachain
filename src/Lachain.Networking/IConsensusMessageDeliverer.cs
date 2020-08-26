using Lachain.Proto;

namespace Lachain.Networking
{
    public interface IConsensusMessageDeliverer
    {
        void SendTo(ECDSAPublicKey publicKey, NetworkMessage networkMessage);
    }
}