using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Google.Protobuf;
using Lachain.Logger;
using Lachain.Proto;
using Lachain.Utility.Utils;

namespace Lachain.Networking.Hub
{
    public class ClientWorker : IDisposable
    {
        private static readonly ILogger<ClientWorker> Logger = LoggerFactory.GetLoggerForClass<ClientWorker>();
        public ECDSAPublicKey PeerPublicKey { get; }
        private readonly IMessageFactory _messageFactory;
        private readonly HubConnector _hubConnector;
        private readonly Thread _worker;
        private bool _isConnected;

        private readonly LinkedList<NetworkMessage> _messageQueue = new LinkedList<NetworkMessage>();
        private long _currentEra = -1;

        private readonly IDictionary<ulong, (MessageBatchContent batch, ulong timestamp)> _unacked =
            new ConcurrentDictionary<ulong, (MessageBatchContent, ulong)>();

        private const int MaxMessageSize = 4000;

        public ClientWorker(ECDSAPublicKey peerPublicKey, IMessageFactory messageFactory, HubConnector hubConnector)
        {
            _messageFactory = messageFactory;
            _hubConnector = hubConnector;
            PeerPublicKey = peerPublicKey;
            _worker = new Thread(Worker);
        }

        public void Start()
        {
            _worker.Start();
        }

        public void Stop()
        {
            _isConnected = false;
            _worker.Join();
        }


        public void Send(NetworkMessage message)
        {
            lock (_messageQueue)
            {
                _messageQueue.AddLast(message);
            }
        }

        private void Worker()
        {
            _isConnected = true;
            while (_isConnected)
            {
                var now = TimeUtils.CurrentTimeMillis();
                List<MessageBatchContent> toSend = new List<MessageBatchContent>();
                var toSendSize = 0;

                lock (_unacked)
                {
                    var cnt = 0;
                    lock (_messageQueue)
                    {
                        foreach (var message in _unacked.Values
                            .Where(x => x.timestamp < now - 5_000)
                            .SelectMany(tuple => tuple.batch.Messages)
                            .Where(msg => msg.MessageCase == NetworkMessage.MessageOneofCase.ConsensusMessage)
                            .Where(msg => msg.ConsensusMessage.Validator.Era >= _currentEra))
                        {
                            _messageQueue.AddFirst(message);
                            ++cnt;
                        }
                    }

                    if (cnt > 0)
                        Logger.LogWarning($"Resubmit {cnt} consensus messages because we got no ack in 5s");

                    foreach (var (key, _) in _unacked
                        .Where(x => x.Value.timestamp < now - 5_000)
                        .ToArray())
                    {
                        _unacked.Remove(key);
                    }
                }

                lock (_messageQueue)
                {
                    var batch = new MessageBatchContent();
                    while (_messageQueue.Count > 0)
                    {
                        var message = _messageQueue.First.Value;
                        if (message.CalculateSize() > MaxMessageSize)
                        {
                            Logger.LogCritical(
                                $"Encountered messaged with size {message.CalculateSize()} > {MaxMessageSize}");
                        }

                        if (message.CalculateSize() + toSendSize > MaxMessageSize) break;
                        _messageQueue.RemoveFirst();
                        batch.Messages.Add(message);
                        toSendSize += message.CalculateSize();
                    }

                    toSend.Add(batch);
                }

                var megaBatchContent = new MessageBatchContent();
                megaBatchContent.Messages.AddRange(toSend.SelectMany(batch => batch.Messages));
                if (megaBatchContent.Messages.Count > 0)
                {
                    var megaBatch = _messageFactory.MessagesBatch(megaBatchContent.Messages);
                    var megaBatchBytes = megaBatch.ToByteArray();
                    if (megaBatchBytes.Length > 4096)
                        Logger.LogWarning(
                            "Attempt to sent message with >4096 bytes it might be not delivered correctly");
                    _hubConnector.Send(PeerPublicKey, megaBatchBytes);
                    _unacked[megaBatch.MessageId] = (megaBatchContent, now);
                }

                var toSleep = Math.Clamp(500 - (long) (TimeUtils.CurrentTimeMillis() - now), 1, 1000);
                Thread.Sleep(TimeSpan.FromMilliseconds(toSleep));
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
            lock (_messageQueue) _messageQueue.Clear();
            Stop();
        }

        public void AdvanceEra(long era)
        {
            _currentEra = era;
        }
    }
}