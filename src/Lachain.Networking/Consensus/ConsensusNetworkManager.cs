using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Lachain.Logger;
using Lachain.Proto;
using Lachain.Utility.Utils;

namespace Lachain.Networking.Consensus
{
    public class ConsensusNetworkManager
    {
        private static readonly ILogger<ConsensusNetworkManager> Logger =
            LoggerFactory.GetLoggerForClass<ConsensusNetworkManager>();

        private readonly IDictionary<ECDSAPublicKey, IncomingPeerConnection> _incoming =
            new ConcurrentDictionary<ECDSAPublicKey, IncomingPeerConnection>();

        private readonly IDictionary<ECDSAPublicKey, OutgoingPeerConnection> _outgoing =
            new ConcurrentDictionary<ECDSAPublicKey, OutgoingPeerConnection>();

        private readonly IDictionary<ECDSAPublicKey, PeerAddress> _peerAddresses;

        private readonly IMessageFactory _messageFactory;
        private readonly Node _localNode;

        public event EventHandler<(MessageEnvelope envelope, ConsensusMessage message)>? OnMessage;

        public ConsensusNetworkManager(IMessageFactory messageFactory, NetworkConfig networkConfig, Node localNode)
        {
            _messageFactory = messageFactory;
            _localNode = localNode;
            _peerAddresses = networkConfig.Peers
                .Select(PeerAddress.Parse)
                .Where(x => x.PublicKey != null)
                .ToDictionary(x => x.PublicKey!);
        }

        public int GetReadyForConnect(ECDSAPublicKey publicKey)
        {
            if (_incoming.TryGetValue(publicKey, out var existingConnection))
                return existingConnection.Port;
            var connection = new IncomingPeerConnection("0.0.0.0", publicKey);
            _incoming[publicKey] = connection;
            connection.OnReceive += SendAck;
            connection.OnAck += ProcessAck;
            connection.OnMessage += HandleConsensusMessage;
            Logger.LogTrace($"Opened port {connection.Port} for peer {publicKey.ToHex()}");
            return connection.Port;
        }

        private void HandleConsensusMessage(object sender, (MessageEnvelope envelope, ConsensusMessage message) e)
        {
            OnMessage?.Invoke(sender, e);
        }

        public void InitOutgoingConnection(ECDSAPublicKey publicKey, PeerAddress address)
        {
            EnsureConnection(publicKey).InitConnection(address);
        }

        private void ProcessAck(object sender, (ECDSAPublicKey publicKey, ulong messageId) message)
        {
            var (publicKey, messageId) = message;
            EnsureConnection(publicKey).ReceiveAck(messageId);
        }

        private void SendAck(object sender, (ECDSAPublicKey publicKey, ulong messageId) message)
        {
            var (publicKey, messageId) = message;
            EnsureConnection(publicKey).Send(_messageFactory.Ack(messageId));
        }

        public void SendTo(ECDSAPublicKey publicKey, NetworkMessage networkMessage)
        {
            EnsureConnection(publicKey).Send(networkMessage);
        }

        private OutgoingPeerConnection EnsureConnection(ECDSAPublicKey key)
        {
            if (_outgoing.TryGetValue(key, out var existingConnection)) return existingConnection;
            if (!_peerAddresses.TryGetValue(key, out var address))
                throw new InvalidOperationException($"Cannot cannot to peer {key}: address not resolved");
            return _outgoing[key] = new OutgoingPeerConnection(address, _messageFactory, _localNode);
        }
    }
}