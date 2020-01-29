using Phorkus.Proto;

namespace Phorkus.Networking
{
    public interface IMessageDeliverer
    {
        void SendTo(ECDSAPublicKey publicKey, NetworkMessage networkMessage);
    }
}