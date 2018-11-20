using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Phorkus.Core.Config;
using Phorkus.Core.Logging;
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
        private readonly ILogger<NetworkManager> _networkLogger;
        private readonly NetworkConfig _networkConfig;

        public Node LocalNode { get; private set; }

        public ConcurrentDictionary<IpEndPoint, IPeer> ActivePeers { get; }
            = new ConcurrentDictionary<IpEndPoint, IPeer>();
        
        public NetworkManager(
            IMessagingManager messagingManager,
            IConfigManager configManager,
            ILogger<NetworkManager> networkLogger,
            ILogger<TcpServer> tcpLogger)
        {
            var networkConfig = configManager.GetConfig<NetworkConfig>("network");
            _messageListener = new MessageListener(this);
            _networkLogger = networkLogger;
            _messagingManager = messagingManager;
            _server = new TcpServer(networkConfig, new DefaultTransport(networkConfig), tcpLogger);
            _networkConfig = networkConfig;
        }

        public void Start()
        {
            if (_server.IsWorking)
                return;

            _networkLogger.LogInformation("Starting network manager");
            LocalNode = new Node
            {
                Version = 0,
                Timestamp = (ulong) DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Services = 0,
                Port = _networkConfig.Port,
                Address = "localhost",
                Nonce = (uint) new Random().Next(1 << 30),
                BlockHeight = 0,
                Agent = "Phorkus-v0.0"
            };
            
            _server.OnPeerAccepted += _PeerConnected;
            _server.OnPeerConnected += _PeerConnected;
            _server.OnPeerClosed += _PeerClosed;
            _messageListener.OnMessageHandled += _MessageHandled;
            _messageListener.OnRateLimited += _RateLimited;
            
            _server.Start();
            
            _networkLogger.LogInformation("Connecting to peers specified (" + _networkConfig.Peers.Length + " peers)");
            Task.Factory.StartNew(() => Parallel.ForEach(_networkConfig.Peers, peer => _server.ConnectTo(IpEndPoint.Parse(peer))));
        }

        public void Stop()
        {
            Parallel.ForEach(ActivePeers.Values, peer => peer.Disconnect());
        }
        
        public void Broadcast(Message message)
        {
            Parallel.ForEach(ActivePeers.Values, peer =>
            {
                try
                {
                    peer.Send(message);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    throw;
                }
            });
        }
        
        private void _PeerConnected(object sender, IPeer peer)
        {
            _networkLogger.LogInformation($"Handled connection with peer ({peer.EndPoint}) with hash code ({peer.GetHashCode()})");
            /* TODO: "also check ACL here" */
            var result = ActivePeers.TryAdd(peer.EndPoint, peer);
            if (!result)
                return;
            peer.OnDisconnect += (s, e) => ActivePeers.TryRemove(peer.EndPoint, out _);
            _messageListener.StartFor(peer, CancellationToken.None);
            Task.Factory.StartNew(peer.Run, TaskCreationOptions.LongRunning); 
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