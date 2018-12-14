using Phorkus.Proto;

namespace Phorkus.Networking
{
    public interface INetworkBroadcaster
    {
        void Broadcast(NetworkMessage networkMessage);
    }
}