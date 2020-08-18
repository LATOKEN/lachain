using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Lachain.Logger;
using Lachain.Proto;
using Lachain.Utility.Benchmark;
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
        private readonly ThroughputCalculator _throughputCalculator;
        private readonly IPeerManager _peerManager;

        public event EventHandler<(ConsensusMessage message, ECDSAPublicKey publicKey)>? OnMessage;

        public ConsensusNetworkManager(IMessageFactory messageFactory, NetworkConfig networkConfig,
            IPeerManager peerManager, Func<string, string> checkLocalConnection)
        {
            _messageFactory = messageFactory;
            _peerManager = peerManager;
            _peerAddresses = networkConfig.Peers?
                .Select(x =>
                {
                    var address = PeerAddress.Parse(x);
                    address.Host = checkLocalConnection(address.Host!);
                    return address;
                })
                .Where(x => x.PublicKey != null)
                .ToDictionary(x => x.PublicKey!)
                ?? throw new Exception("No peers specified in network config");

            _throughputCalculator = new ThroughputCalculator(
                TimeSpan.FromSeconds(1),
                (speed, cnt) =>
                    Logger.LogDebug(
                        $"Outgoing bandwidth: {speed / 1024:0.00} KiB/s, {cnt} messages"
                    )
            );
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public int GetReadyForConnect(ECDSAPublicKey publicKey)
        {
            lock (_incoming)
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
        }

        public void HandleConsensusMessage(object sender, (ConsensusMessage message, ECDSAPublicKey publicKey) e)
        {
            OnMessage?.Invoke(sender, e);
        }

        private void ProcessAck(object sender, (ECDSAPublicKey publicKey, ulong messageId) message)
        {
            var (publicKey, messageId) = message;
            EnsureConnection(publicKey)?.ReceiveAck(messageId);
        }

        private void SendAck(object sender, (ECDSAPublicKey publicKey, ulong messageId) message)
        {
            var (publicKey, messageId) = message;
            var ack = _messageFactory.Ack(messageId);
            Logger.LogTrace($"Sending ack {messageId} for {publicKey.ToHex()}");
            _throughputCalculator.RegisterMeasurement(ack.CalculateSize());
            EnsureConnection(publicKey)?.Send(ack);
        }

        public void SendTo(ECDSAPublicKey publicKey, NetworkMessage networkMessage)
        {
            _throughputCalculator.RegisterMeasurement(networkMessage.CalculateSize());
            EnsureConnection(publicKey)?.Send(networkMessage);
        }

        private OutgoingPeerConnection? EnsureConnection(ECDSAPublicKey key)
        {
            lock (_outgoing)
            {
                if (_outgoing.TryGetValue(key, out var existingConnection)) return existingConnection;
                if (!_peerAddresses.TryGetValue(key, out var address))
                {
                    var newValidatorPeer = _peerManager.GetPeerAddressByPublicKey(key);
                    if (newValidatorPeer == null)
                    {
                        Logger.LogWarning($"Cannot connect to peer {key.ToHex()}: address not resolved");
                        return null;
                    }

                    _peerAddresses.Add(key, newValidatorPeer);
                }

                return _outgoing[key] = new OutgoingPeerConnection(address, _messageFactory);
            }
        }

        private void RemoveOddPeers(ICollection<ECDSAPublicKey> keys)
        {
            lock (_peerAddresses)
            {
                foreach (var key in _peerAddresses.Keys)
                    if (!keys.Contains(key))
                        _peerAddresses.Remove(key);
            }
        }

        public void ConnectToValidators(IEnumerable<ECDSAPublicKey> validators)
        {
            var validatorsSet = validators.ToHashSet();
            lock (_outgoing)
            {
                foreach (var connection in _outgoing.Values)
                {
                    connection.Dispose();
                }

                _outgoing.Clear();
            }

            lock (_incoming)
            {
                var toDel = new List<ECDSAPublicKey>();
                foreach (var key in _incoming.Keys)
                {
                    if (validatorsSet.Contains(key)) continue;
                    _incoming[key].Dispose();
                    toDel.Add(key);
                }

                foreach (var key in toDel)
                    _incoming.Remove(key);
            }

            RemoveOddPeers(validatorsSet);

            foreach (var validator in validatorsSet)
            {
                EnsureConnection(validator);
            }
        }
    }
}