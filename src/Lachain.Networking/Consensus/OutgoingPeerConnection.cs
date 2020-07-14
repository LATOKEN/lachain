using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Lachain.Logger;
using Lachain.Networking.ZeroMQ;
using Lachain.Proto;
using Lachain.Utility;
using Lachain.Utility.Utils;

namespace Lachain.Networking.Consensus
{
    public class OutgoingPeerConnection : IDisposable
    {
        private static readonly ILogger<OutgoingPeerConnection> Logger =
            LoggerFactory.GetLoggerForClass<OutgoingPeerConnection>();

        private ClientWorker? _client;

        private IDictionary<ulong, (NetworkMessage msg, ulong timestamp)> _unacked =
            new ConcurrentDictionary<ulong, (NetworkMessage, ulong)>();

        private readonly IList<NetworkMessage> _unsent = new List<NetworkMessage>();
        private Thread? _unackedWorker;
        private ThroughputCalculator _throughputCalculator;

        public OutgoingPeerConnection(PeerAddress address, IMessageFactory messageFactory, Node localNode)
        {
            _throughputCalculator = new ThroughputCalculator(
                TimeSpan.FromSeconds(1),
                (speed, cnt) =>
                    Logger.LogDebug(
                        $"Outgoing bandwidth to peer {address}: {speed / 1024:0.00} KiB/s, {cnt} messages"
                    )
            );
            ClientWorker.SendOnce(address, messageFactory.HandshakeRequest(localNode));
        }

        public void InitConnection(PeerAddress address)
        {
            if (_client != null) return;
            Logger.LogTrace($"Initiating consensus connection to peer {address}");
            _client = new ClientWorker(address, address.PublicKey);
            _client.Start();
            lock (_unsent)
            {
                Logger.LogTrace($"Sending {_unsent.Count} unsent messages to {address}");
                foreach (var msg in _unsent) Send(msg);
                _unsent.Clear();
            }

            _unackedWorker = new Thread(UnackedChecker);
            _unackedWorker.Start();
        }

        public void Send(NetworkMessage message)
        {
            if (message.MessageCase != NetworkMessage.MessageOneofCase.Ack)
            {
                lock (_unacked)
                {
                    _unacked[message.MessageId] = (message, TimeUtils.CurrentTimeMillis());
                }
            }

            if (_client is null)
            {
                lock (_unsent)
                {
                    _unsent.Add(message);
                }
            }
            else
            {
                _throughputCalculator.RegisterMeasurement(message.CalculateSize());
                _client.Send(message);
            }
        }

        public void ReceiveAck(ulong messageId)
        {
            lock (_unacked)
            {
                _unacked.Remove(messageId);
            }
        }

        public void Dispose()
        {
            _unackedWorker?.Interrupt();
            _unackedWorker?.Join();
            _client?.Dispose();
        }

        private void UnackedChecker()
        {
            while (true)
            {
                Thread.Sleep(TimeSpan.FromMilliseconds(5_000));
                NetworkMessage[] toResend;
                lock (_unacked)
                {
                    var cnt = _unacked.Count;
                    if (cnt == 0) continue;
                    Logger.LogTrace($"Got {cnt} unacked messages, resending");
                    var now = TimeUtils.CurrentTimeMillis();
                    toResend = _unacked.Values
                        .Where(x => x.timestamp < now - 5_000)
                        .Where(x => x.timestamp > now - 30_000)
                        .Select(x => x.msg)
                        .ToArray();
                }


                foreach (var msg in toResend)
                {
                    Send(msg);
                }
            }
        }

        public void ClearUnackedForEra(long era)
        {
            lock (_unacked)
            {
                _unacked = _unacked.Where(x =>
                        x.Value.msg.MessageCase != NetworkMessage.MessageOneofCase.ConsensusMessage ||
                        x.Value.msg.ConsensusMessage.Validator.Era < era)
                    .ToDictionary(x => x.Key, x => x.Value);
            }
        }
    }
}