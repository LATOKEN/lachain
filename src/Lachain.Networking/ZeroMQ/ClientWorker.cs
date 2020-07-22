using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Google.Protobuf;
using NetMQ;
using NetMQ.Sockets;
using Lachain.Proto;
using Lachain.Logger;
using Lachain.Utility.Utils;

namespace Lachain.Networking.ZeroMQ
{
    public class ClientWorker : IRemotePeer, IDisposable
    {
        private static readonly ILogger<ClientWorker> Logger = LoggerFactory.GetLoggerForClass<ClientWorker>();

        public bool IsConnected { get; private set; }
        public PeerAddress Address { get; }
        public ECDSAPublicKey? PublicKey { get; }
        public DateTime Connected { get; } = DateTime.Now;

        private readonly IMessageFactory _messageFactory;
        private readonly PushSocket _socket;
        private readonly Thread _worker;

        private readonly IDictionary<ulong, (MessageBatch msg, ulong timestamp)> _unacked =
            new ConcurrentDictionary<ulong, (MessageBatch, ulong)>();

        public ClientWorker(PeerAddress peerAddress, ECDSAPublicKey? publicKey, IMessageFactory messageFactory)
        {
            _messageFactory = messageFactory;
            PublicKey = publicKey;
            Address = peerAddress;
            _socket = new PushSocket();
            _worker = new Thread(Worker);
        }

        public void Start()
        {
            _worker.Start();
        }

        public void Stop()
        {
            IsConnected = false;
            _worker.Join();
        }

        public static void SendOnce(PeerAddress address, MessageBatch message)
        {
            Task.Factory.StartNew(() =>
            {
                var endpoint = $"tcp://{address.Host}:{address.Port}";
                using var socket = new PushSocket();
                socket.Connect(endpoint);
                socket.SendFrame(message.ToByteArray());
                Thread.Sleep(TimeSpan.FromMilliseconds(1000)); // TODO: wtf?                
            });
        }

        private readonly Queue<NetworkMessage> _messageQueue = new Queue<NetworkMessage>();

        public void Send(NetworkMessage message)
        {
            lock (_messageQueue)
            {
                _messageQueue.Enqueue(message);
            }
        }

        private void Worker()
        {
            var endpoint = $"tcp://{Address.Host}:{Address.Port}";
            _socket.Connect(endpoint);
            IsConnected = true;
            while (IsConnected)
            {
                var now = TimeUtils.CurrentTimeMillis();
                List<MessageBatch> toSend = new List<MessageBatch>();
                lock (_messageQueue)
                {
                    if (_messageQueue.Count != 0)
                    {
                        var content = _messageQueue.ToArray();
                        _messageQueue.Clear();
                        var batch = _messageFactory.MessagesBatch(content);
                        if (content.Any(msg => msg.MessageCase != NetworkMessage.MessageOneofCase.Ack))
                            _unacked[batch.MessageId] = (batch, now);
                        toSend.Add(batch);
                    }
                }

                lock (_unacked)
                {
                    foreach (var msg in _unacked.Values
                        .Where(x => x.timestamp < now - 5_000)
                        .Where(x => x.timestamp > now - 30_000)
                        .Select(t => t.msg))
                    {
                        _unacked[msg.MessageId] = (msg, now);
                        toSend.Add(msg);
                    }
                }

                foreach (var msg in toSend)
                {
                    _socket.SendFrame(msg.ToByteArray());
                }

                var toSleep = 500 - (long) (TimeUtils.CurrentTimeMillis() - now);
                if (toSleep <= 0) toSleep = 1;
                if (toSleep > 1000) toSleep = 1000;
                Thread.Sleep(TimeSpan.FromMilliseconds(toSleep));
            }

            _socket.Disconnect(endpoint);
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
            Stop();
            _socket.Dispose();
        }
    }
}