using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Phorkus.Core.Handling;
using Phorkus.Core.Network.Proto;
using Phorkus.Core.Proto;

namespace Phorkus.Core.Network
{
    public class NetworkManager : INetworkManager, INetworkContext
    {
        private readonly IMessageListener _messageListener;
        private readonly IHandlingManager _handlingManager;
        private readonly IServer _server;
        private readonly NetworkConfig _networkConfig;

        public Node LocalNode { get; private set; }

        public ConcurrentDictionary<IpEndPoint, IPeer> ActivePeers { get; }
            = new ConcurrentDictionary<IpEndPoint, IPeer>();
        
        public NetworkManager(
            IMessageListener messageListener,
            IHandlingManager handlingManager,
            IServer server,
            NetworkConfig networkConfig)
        {
            _messageListener = messageListener;
            _handlingManager = handlingManager;
            _server = server;
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

            foreach (var ipEndPoint in _networkConfig.PeerEndPoints)
                _server.ConnectTo(ipEndPoint);
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
            _handlingManager.HandleMessage(peer, message);
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