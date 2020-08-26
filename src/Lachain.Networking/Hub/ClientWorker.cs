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

        private readonly Queue<NetworkMessage> _messageQueue = new Queue<NetworkMessage>();
        private long _currentEra = -1;

        private readonly IDictionary<ulong, (MessageBatchContent batch, ulong timestamp)> _unacked =
            new ConcurrentDictionary<ulong, (MessageBatchContent, ulong)>();

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
                _messageQueue.Enqueue(message);
            }
        }

        private void Worker()
        {
            _isConnected = true;
            while (_isConnected)
            {
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
                }

                var megaBatchContent = new MessageBatchContent();
                megaBatchContent.Messages.AddRange(toSend.SelectMany(batch => batch.Messages));
                var megaBatch = _messageFactory.MessagesBatch(megaBatchContent.Messages);
                var megaBatchBytes = megaBatch.ToByteArray();
                _hubConnector.Send(PeerPublicKey, megaBatchBytes);
                _unacked[megaBatch.MessageId] = (megaBatchContent, now);

                var toSleep = Math.Clamp(500 - (long) (TimeUtils.CurrentTimeMillis() - now), 1, 1000);
                Thread.Sleep(TimeSpan.FromMilliseconds(toSleep));
            }
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
        }

        public void AdvanceEra(long era)
        {
            _currentEra = era;
        }
    }
}