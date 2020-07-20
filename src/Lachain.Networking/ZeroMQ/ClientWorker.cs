using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
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
        private ulong _batchTs;

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
            lock (_queueNotEmpty) Monitor.Pulse(_queueNotEmpty);
            _worker.Join();
        }

        public static void SendOnce(PeerAddress address, MessageBatch message)
        {
            var endpoint = $"tcp://{address.Host}:{address.Port}";
            using var socket = new PushSocket();
            socket.Connect(endpoint);
            socket.SendFrame(message.ToByteArray());
            Thread.Sleep(TimeSpan.FromMilliseconds(10)); // TODO: wtf?
        }

        private readonly Queue<NetworkMessage> _messageQueue = new Queue<NetworkMessage>();
        private readonly object _queueNotEmpty = new object();

        public void Send(NetworkMessage message)
        {
            lock (_messageQueue)
            {
                _messageQueue.Enqueue(message);
            }

            lock (_queueNotEmpty)
            {
                Monitor.Pulse(_queueNotEmpty);
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
                    if (now - _batchTs > 1_000 && _messageQueue.Count != 0)
                    {
                        var batch = _messageFactory.MessagesBatch(_messageQueue.ToArray());
                        _unacked[batch.MessageId] = (batch, now);
                        toSend.Add(batch);
                        _messageQueue.Clear();
                        _batchTs = now;
                    }
                }

                lock (_unacked)
                {
                    var cnt = _unacked.Count;
                    if (cnt == 0) continue;
                    Logger.LogTrace($"Got {cnt} unacked messages, resending");
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