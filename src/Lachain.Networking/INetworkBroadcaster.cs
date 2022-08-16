using Lachain.Proto;

namespace Lachain.Networking
{
    public interface INetworkBroadcaster
    {
        void Broadcast(NetworkMessage networkMessage, bool priorityMessage);
    }
}