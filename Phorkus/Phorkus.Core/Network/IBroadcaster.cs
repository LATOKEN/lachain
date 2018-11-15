using Phorkus.Core.Network.Proto;

namespace Phorkus.Core.Network
{
    public interface IBroadcaster
    {
        void Broadcast(Message message);
    }
}