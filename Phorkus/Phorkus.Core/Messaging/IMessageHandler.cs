using Phorkus.Core.Network;
using Phorkus.Core.Network.Proto;

namespace Phorkus.Core.Messaging
{
    public interface IMessageHandler
    {
        void HandleMessage(IPeer peer, Message message);
    }
}