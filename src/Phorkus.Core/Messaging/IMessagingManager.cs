using Phorkus.Core.Network;
using Phorkus.Core.Network.Proto;

namespace Phorkus.Core.Messaging
{
    public interface IMessagingManager
    {
        void HandleMessage(IPeer peer, Message message);
    }
}