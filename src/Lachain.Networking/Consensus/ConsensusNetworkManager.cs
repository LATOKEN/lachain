using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Linq;
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

        private readonly IDictionary<ECDSAPublicKey, OutgoingPeerConnection> _outgoing =
            new ConcurrentDictionary<ECDSAPublicKey, OutgoingPeerConnection>();

        private readonly IDictionary<ECDSAPublicKey, PeerAddress> _peerAddresses;

        private readonly IMessageFactory _messageFactory;
        private readonly ThroughputCalculator _throughputCalculator;
        private readonly IPeerManager _peerManager;

        public ConsensusNetworkManager(IMessageFactory messageFactory, NetworkConfig networkConfig,
            IPeerManager peerManager, Func<string, string> checkLocalConnection)
        {
            _messageFactory = messageFactory;
            _peerManager = peerManager;
            if (networkConfig.Peers is null) throw new NoNullAllowedException("No peers specified in network config");
            _peerAddresses = networkConfig.Peers
                .Select(x =>
                    {
                        var address = PeerAddress.Parse(x);
                        address.Host = checkLocalConnection(address.Host!);
                        return address;
                    }
                )
                .Where(x => x.PublicKey != null)
                .ToDictionary(x => x.PublicKey!);
            _throughputCalculator = new ThroughputCalculator(
                TimeSpan.FromSeconds(1),
                (speed, cnt) =>
                    Logger.LogDebug(
                        $"Outgoing bandwidth: {speed / 1024:0.00} KiB/s, {cnt} messages"
                    )
            );
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
                if (_peerAddresses.TryGetValue(key, out var address))
                    return _outgoing[key] = new OutgoingPeerConnection(address, _messageFactory);
                var newValidatorPeer = _peerManager.GetPeerAddressByPublicKey(key);
                if (newValidatorPeer == null)
                {
                    Logger.LogWarning($"Cannot connect to peer {key.ToHex()}: address not resolved");
                    return null;
                }

                _peerAddresses.Add(key, newValidatorPeer);

                return _outgoing[key] = new OutgoingPeerConnection(address, _messageFactory);
            }
        }

        public void AdvanceEra(long era)
        {
            foreach (var connection in _outgoing.Values)
                connection.AdvanceEra(era);
        }
    }
}