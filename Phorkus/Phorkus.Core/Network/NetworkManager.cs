using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Phorkus.Core.Config;
using Phorkus.Core.Messaging;
using Phorkus.Core.Network.Proto;
using Phorkus.Core.Network.Tcp;
using Phorkus.Core.Proto;

namespace Phorkus.Core.Network
{
    public class NetworkManager : INetworkManager, INetworkContext
    {
        private readonly IMessageListener _messageListener;
        private readonly IMessagingManager _messagingManager;
        private readonly IServer _server;
        private readonly NetworkConfig _networkConfig;

        public Node LocalNode { get; private set; }

        public ConcurrentDictionary<IpEndPoint, IPeer> ActivePeers { get; }
            = new ConcurrentDictionary<IpEndPoint, IPeer>();
        
        public NetworkManager(
            IMessagingManager messagingManager,
            IConfigManager configManager)
        {
            var networkConfig = configManager.GetConfig<NetworkConfig>("network");
            _messageListener = new MessageListener();
            _messagingManager = messagingManager;
            _server = new TcpServer(networkConfig, new DefaultTransport(networkConfig));
            _networkConfig = networkConfig;
        }

        public void Start()
        {
            if (_server.IsWorking)
                return;

            LocalNode = new Node
            {
                Version = 0,
                Timestamp = (ulong) DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Services = 0,
                Port = _networkConfig.Port,
                Address = null,
                Nonce = (uint) new Random().Next(1 << 30),
                BlockHeight = 0,
                Agent = "Phorkus-v0.0"
            };
            
            _server.OnPeerConnected += _PeerConnected;
            _server.OnPeerClosed += _PeerClosed;
            _messageListener.OnMessageHandled += _MessageHandled;
            _messageListener.OnRateLimited += _RateLimited;
            
            _server.Start();

            foreach (var peer in _networkConfig.Peers)
                _server.ConnectTo(IpEndPoint.Parse(peer));
        }

        public void Stop()
        {
            Parallel.ForEach(ActivePeers.Values, peer => peer.Disconnect());
        }
        
        public void Broadcast(Message message)
        {
            Parallel.ForEach(ActivePeers.Values, peer => peer.Send(message));
        }
        
        private void _PeerConnected(object sender, IPeer peer)
        {
            /* TODO: "also check ACL here" */
            var result = ActivePeers.TryAdd(peer.EndPoint, peer);
            if (!result)
                return;
            peer.OnDisconnect += (s, e) => ActivePeers.TryRemove(peer.EndPoint, out _);
            _messageListener.StartFor(peer, CancellationToken.None);
        }
        
        private void _MessageHandled(object sender, Message message)
        {
            if (!(sender is IPeer peer))
                throw new ArgumentNullException(nameof(peer));
            _messagingManager.HandleMessage(peer, message);
        }
        
        private void _RateLimited(object sender, IPeer peer)
        {
            /* TODO: "disconnect peer here" */
        }
        
        private void _PeerClosed(object sender, IPeer peer)
        {
            ActivePeers.TryRemove(peer.EndPoint, out _);
        }
    }
}