using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Phorkus.Core.Config;
using Phorkus.Core.Logging;
using Phorkus.Core.Messaging;
using Phorkus.Proto;
using Phorkus.Core.Network.Tcp;
using Phorkus.Proto;

namespace Phorkus.Core.Network
{
    public class NetworkManager : INetworkManager
    {
        private readonly IMessageListener _messageListener;
        private readonly IMessagingManager _messagingManager;
        private readonly IServer _server;
        private readonly ILogger<NetworkManager> _networkLogger;
        private readonly INetworkContext _networkContext;
        private readonly NetworkConfig _networkConfig;

        public NetworkManager(
            IMessagingManager messagingManager,
            IConfigManager configManager,
            ILogger<NetworkManager> networkLogger,
            ILogger<TcpServer> tcpLogger,
            ILogger<MessageListener> listenerLogger,
            INetworkContext networkContext
        )
        {
            var networkConfig = configManager.GetConfig<NetworkConfig>("network");
            _networkContext = networkContext;
            _messageListener = new MessageListener(_networkContext, listenerLogger);
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

            _server.OnPeerConnected += _PeerConnected;
            _server.OnPeerAccepted += _PeerConnected;
            _server.OnPeerClosed += _PeerClosed;
            _messageListener.OnMessageHandled += _MessageHandled;
            _messageListener.OnRateLimited += _RateLimited;

            _server.Start();

            _networkLogger.LogInformation("Connecting to peers specified (" + _networkConfig.Peers.Length + " peers)");
            Task.Factory.StartNew(() =>
                Parallel.ForEach(_networkConfig.Peers, peer => _server.ConnectTo(IpEndPoint.Parse(peer))));
            
        }

        public void Stop()
        {
            Parallel.ForEach(_networkContext.ActivePeers.Values, peer => peer.Disconnect());
        }

        private void _PeerConnected(object sender, IPeer peer)
        {
            _networkLogger.LogInformation(
                $"Handled connection with peer ({peer.EndPoint}) with hash code ({peer.GetHashCode()})");
            /* TODO: "also check ACL here" */
            var result = _networkContext.ActivePeers.TryAdd(peer.EndPoint, peer);
            if (!result)
                return;
            peer.OnDisconnect += (s, e) => _networkContext.ActivePeers.TryRemove(peer.EndPoint, out _);
            _messageListener.StartFor(peer, CancellationToken.None);
            Task.Factory.StartNew(peer.Run, TaskCreationOptions.LongRunning);
            Task.Factory.StartNew(peer.Run, TaskCreationOptions.LongRunning);
            var message = new Message
            {
                Type = MessageType.HandshakeRequest,
                HandshakeRequest = new HandshakeRequestMessage
                {
                    Node = _networkContext.LocalNode
                }
            };
            peer.Send(message);
        }

        private void _MessageHandled(object sender, Message message)
        {
            if (!(sender is IPeer peer))
                throw new ArgumentNullException(nameof(peer));
            try
            {
                if (!_messagingManager.HandleMessage(peer, message))
                    _networkLogger.LogWarning($"Unable to handle message ({message.Type})");
            }
            catch (Exception e)
            {
                _networkLogger.LogError($"Unable to handle message {message.Type}: {e}");
            }
        }

        private void _RateLimited(object sender, IPeer peer)
        {
            /* TODO: "disconnect peer here" */
        }

        private void _PeerClosed(object sender, IPeer peer)
        {
            _networkContext.ActivePeers.TryRemove(peer.EndPoint, out _);
        }
    }
}