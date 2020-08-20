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
using Lachain.Networking.Hub;
using Lachain.Utility.Utils;

namespace Lachain.Networking.ZeroMQ
{
    public class ClientWorker : IRemotePeer, IDisposable
    {
        private static readonly ILogger<ClientWorker> Logger = LoggerFactory.GetLoggerForClass<ClientWorker>();

        public bool IsConnected { get; private set; }
        public PeerAddress Address { get; }
        public ECDSAPublicKey? PeerPublicKey { get; }
        public DateTime Connected { get; } = DateTime.Now;

        private readonly IMessageFactory _messageFactory;
        private readonly PushSocket _socket;
        private readonly Thread _worker;

        private readonly IDictionary<ulong, (MessageBatchContent batch, ulong timestamp)> _unacked =
            new ConcurrentDictionary<ulong, (MessageBatchContent, ulong)>();

        private long _currentEra = -1;

        public ClientWorker(PeerAddress peerAddress, ECDSAPublicKey? peerPublicKey, IMessageFactory messageFactory)
        {
            _messageFactory = messageFactory;
            PeerPublicKey = peerPublicKey;
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
                Logger.LogTrace($"{Address}: unacked {_unacked.Count}, queue: {_messageQueue.Count}");
                var now = TimeUtils.CurrentTimeMillis();
                List<MessageBatchContent> toSend = new List<MessageBatchContent>();
                lock (_messageQueue)
                {
                    if (_messageQueue.Count != 0)
                    {
                        var content = _messageQueue.ToArray();
                        _messageQueue.Clear();
                        var batch = new MessageBatchContent();
                        batch.Messages.AddRange(content);
                        toSend.Add(batch);
                    }
                }

                lock (_unacked)
                {
                    var batch = new MessageBatchContent();
                    batch.Messages.AddRange(_unacked.Values
                        .Where(x => x.timestamp < now - 5_000)
                        .SelectMany(tuple => tuple.batch.Messages)
                        .Where(msg => msg.MessageCase == NetworkMessage.MessageOneofCase.ConsensusMessage)
                        .Where(msg => msg.ConsensusMessage.Validator.Era >= _currentEra)
                    );
                    if (batch.Messages.Count > 0)
                    {
                        Logger.LogWarning(
                            $"Resubmit {batch.Messages.Count} consensus messages because we got no ack in 5s");
                        toSend.Add(batch);
                    }

                    var hubBatchContent = new MessageBatchContent();
                    hubBatchContent.Messages.AddRange(
                        _unacked
                            .Where(x => x.Value.timestamp < now - 5_000)
                            .SelectMany(x => x.Value.batch.Messages)
                    );
                    if (PeerPublicKey != null && hubBatchContent.Messages.Count > 0)
                    {
                        var hubBatch = _messageFactory.MessagesBatch(hubBatchContent.Messages);
                        var hubBatchBytes = hubBatch.ToByteArray();
                        Logger.LogTrace(
                            $"Sending batch {hubBatch.MessageId} via communication hub" +
                            $" with {hubBatchContent.Messages.Count} messages," +
                            $" total {hubBatchBytes.Length} bytes"
                        );

                        CommunicationHub.Send(
                            _messageFactory.GetPublicKey(), PeerPublicKey, hubBatchBytes,
                            _messageFactory.SignCommunicationHubSend(PeerPublicKey, hubBatchBytes)
                        );
                    }


                    foreach (var msgId in _unacked
                        .Where(x => x.Value.timestamp < now - 5_000)
                        .Select(x => x.Key)
                        .ToArray()
                    )
                    {
                        _unacked.Remove(msgId);
                    }
                }

                var megaBatchContent = new MessageBatchContent();
                megaBatchContent.Messages.AddRange(toSend.SelectMany(batch => batch.Messages));
                var megaBatch = _messageFactory.MessagesBatch(megaBatchContent.Messages);
                var megaBatchBytes = megaBatch.ToByteArray();
                Logger.LogTrace(
                    $"Sending batch {megaBatch.MessageId}" +
                    $" with {megaBatchContent.Messages.Count} messages," +
                    $" total {megaBatchBytes.Length} bytes"
                );
                _socket.SendFrame(megaBatchBytes);
                _unacked[megaBatch.MessageId] = (megaBatchContent, now);

                var toSleep = Math.Clamp(500 - (long) (TimeUtils.CurrentTimeMillis() - now), 1, 1000);
                Thread.Sleep(TimeSpan.FromMilliseconds(toSleep));
            }

            _socket.Disconnect(endpoint);
        }

        public void ReceiveAck(ulong messageId)
        {
            lock (_unacked)
            {
                Logger.LogTrace($"Got ack for message {messageId}");
                _unacked.Remove(messageId);
            }
        }

        public void Dispose()
        {
            lock (_messageQueue) _messageQueue.Clear();
            Stop();
            _socket.Dispose();
        }

        public void AdvanceEra(long era)
        {
            _currentEra = era;
        }
    }
}