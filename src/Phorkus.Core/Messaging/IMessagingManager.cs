using Phorkus.Core.Network;
using Phorkus.Proto;

namespace Phorkus.Core.Messaging
{
    public interface IMessagingManager
    {
        void HandleMessage(IPeer peer, Message message);
    }
}