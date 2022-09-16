using Lachain.Proto;
using Lachain.Utility;

namespace Lachain.Networking
{
    public interface INetworkBroadcaster
    {
        void Broadcast(NetworkMessage networkMessage, NetworkMessagePriority priority);
    }
}