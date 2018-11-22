using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Phorkus.Core.Logging;
using Phorkus.Proto;
using Phorkus.Core.Utils;

namespace Phorkus.Core.Network
{
    public class MessageListener : IMessageListener
    {
        private readonly ConcurrentDictionary<IPeer, ulong> _messageCount = new ConcurrentDictionary<IPeer, ulong>();
        private readonly ConcurrentDictionary<IPeer, ulong> _lastMessage = new ConcurrentDictionary<IPeer, ulong>();
        
        public event EventHandler<Message> OnMessageHandled;
        public event EventHandler<IPeer> OnRateLimited;

        private readonly ILogger<MessageListener> _logger;
        private readonly INetworkContext _networkContext;

        public MessageListener(INetworkContext networkContext, ILogger<MessageListener> logger)
        {
            _networkContext = networkContext ?? throw new ArgumentNullException(nameof(networkContext));
            _logger = logger;
        }
        
        private void _Worker(IPeer peer, CancellationToken cancellationToken)
        {
            _logger.LogInformation($"Starting message listener for peer {peer.EndPoint}");
            while (peer.IsConnected && !cancellationToken.IsCancellationRequested)
            {
                /* read array with messages from peer */
                var messages = peer.Receive();
                if (messages == null)
                    continue;
                /* convert messages to array */
                var array = messages as Message[] ?? messages.ToArray();
                if (array.Length == 0)
                    continue;
                /* handshake unknown peer */
                if (!peer.IsKnown && !_TryHandshake(peer, array))
                    break;
                /* handle messages */
                foreach (var message in array)
                {
                    if (message.Type == MessageType.HandshakeRequest ||
                        message.Type == MessageType.HandshakeResponse)
                    {
                        continue;
                    }
                    OnMessageHandled?.Invoke(peer, message);
                }
                /* refresh message counts (for rate limitations in future) */
                _CheckRatePolicy(peer, (uint) array.Length);
            }
            _logger.LogInformation($"Terminating message listener for peer {peer.EndPoint}");
            peer.Disconnect();
        }
        
        private void _CheckRatePolicy(IPeer peer, uint messageCount)
        {
            var currentTime = TimeUtils.CurrentTimeMillis();
            var lastTime = _lastMessage.GetOrAdd(peer, currentTime);
            if (lastTime == 0)
                lastTime = currentTime;
            var deltaTime = currentTime - lastTime;
            var messages = _messageCount.GetOrAdd(peer, 0) + messageCount;
            /* try to check rate limit every second */
            if (deltaTime > 1000 && 60 * 1000 * messages / deltaTime > peer.RateLimit)
                OnRateLimited?.Invoke(this, peer);
            _messageCount[peer] = messages;
            _lastMessage[peer] = currentTime;
        }
        
        private bool _TryHandshake(IPeer peer, Message[] messages)
        {
            if (messages.Length == 0)
                throw new ArgumentOutOfRangeException(nameof(messages));
            /* first message should be handshake for unknown peer */
            var message = messages[0];
            if (message.Type == MessageType.HandshakeRequest)
            {
                var handshake = message.HandshakeRequest;
                if (handshake is null || !handshake.Node.IsValid())
                    return false;
                peer.Node = handshake.Node;
                peer.IsKnown = true;
                var answer = new Message
                {
                    Type = MessageType.HandshakeResponse,
                    HandshakeResponse = new HandshakeResponseMessage { Node = _networkContext.LocalNode }
                };
                _logger.LogInformation($"Handshaked client {peer.Node}");
                peer.Send(answer);
                return true;
            }
            if (message.Type == MessageType.HandshakeResponse)
            {
                var handshake = message.HandshakeResponse;
                if (handshake is null || !handshake.Node.IsValid())
                    return false;
                peer.Node = handshake.Node;
                peer.IsKnown = true;
                _logger.LogInformation($"Handshaked client {peer.Node}");
                return true;
            }
            return false;
        }
        
        public void StartFor(IPeer peer, CancellationToken cancellationToken)
        {
            Task.Factory.StartNew(() =>
            {
                while (true)
                {
                    try
                    {
                        _Worker(peer, cancellationToken);
                        break;
                    }
                    catch (Exception e)
                    {
                        _logger.LogError("Message listener's worker has failed", e);
                    }
                }
            }, cancellationToken, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        }
    }
}