using Phorkus.Core.Network;
using Phorkus.Proto;

namespace Phorkus.Core.Messaging
{
    public interface IMessageHandler
    {
        void HandleMessage(IPeer peer, Message message);
    }
}