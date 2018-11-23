using Phorkus.Core.Network;
using Phorkus.Proto;

namespace Phorkus.Core.Messaging.Handlers
{
    public class GetNeighboursMessageHandler : IMessageHandler
    {
        private readonly INetworkContext _networkContext;
        
        public void HandleMessage(IPeer peer, Message message)
        {
            
        }
    }
}