using Lachain.Proto;

namespace Lachain.Networking
{
    public interface IMessageDeliverer
    {
        void SendTo(ECDSAPublicKey publicKey, NetworkMessage networkMessage);
    }
}