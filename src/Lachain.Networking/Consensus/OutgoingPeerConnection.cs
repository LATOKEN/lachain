using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Lachain.Logger;
using Lachain.Networking.ZeroMQ;
using Lachain.Proto;

namespace Lachain.Networking.Consensus
{
    public class OutgoingPeerConnection : IDisposable
    {
        private static readonly ILogger<OutgoingPeerConnection> Logger =
            LoggerFactory.GetLoggerForClass<OutgoingPeerConnection>();

        private ClientWorker? _client;

        private readonly IDictionary<ulong, NetworkMessage>
            _unacked = new ConcurrentDictionary<ulong, NetworkMessage>(); // TODO: resend unacked from time to time

        private readonly IList<NetworkMessage> _unsent = new List<NetworkMessage>();
        private Thread? _unackedWorker;

        public OutgoingPeerConnection(PeerAddress address, IMessageFactory messageFactory, Node localNode)
        {
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
                    _unacked[message.MessageId] = message;                    
                }
            }
                
            if (_client is null)
            {
                lock (_unsent)
                {
                    _unsent.Add(message);
                }
            }
            else _client.Send(message);
        }

        public void ReceiveAck(ulong messageId)
        {
            _unacked.Remove(messageId);
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
                Thread.Sleep(TimeSpan.FromMilliseconds(1_000));
                NetworkMessage[] toResend;
                lock (_unacked)
                {
                    var cnt = _unacked.Count;
                    if (cnt == 0) continue;
                    Logger.LogTrace($"Got {cnt} unacked messages, resending");
                    toResend = _unacked.Values.ToArray();
                }

                foreach (var msg in toResend)
                {
                    Send(msg);
                }
            }
        }
    }
}