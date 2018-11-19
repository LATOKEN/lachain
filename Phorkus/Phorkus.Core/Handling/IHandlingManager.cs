using Phorkus.Core.Network;
using Phorkus.Core.Network.Proto;

namespace Phorkus.Core.Handling
{
    public interface IHandlingManager
    {
        void HandleMessage(IPeer peer, Message message);
    }
}