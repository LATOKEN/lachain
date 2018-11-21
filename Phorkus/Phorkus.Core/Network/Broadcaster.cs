using System;
using System.Threading.Tasks;
using Phorkus.Core.Logging;
using Phorkus.Core.Network.Proto;

namespace Phorkus.Core.Network
{
    public class Broadcaster : IBroadcaster
    {
        private readonly INetworkContext _networkContext;
        private readonly ILogger<Broadcaster> _logger;

        public Broadcaster(INetworkContext networkContext, ILogger<Broadcaster> logger)
        {
            _networkContext = networkContext;
            _logger = logger;
        }
        
        
        public void Broadcast(Message message)
        {
            Parallel.ForEach(_networkContext.ActivePeers.Values, peer =>
            {
                try
                {
                    peer.Send(message);
                }
                catch (Exception e)
                {
                    _logger.LogError($"Error while broadcasting to {peer.EndPoint}: {e}");
                    throw;
                }
            });
        }
    }
}