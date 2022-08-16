using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Google.Protobuf;
using Lachain.Consensus;
using Lachain.Crypto;
using Lachain.Logger;
using Lachain.Proto;
using Lachain.Utility.Utils;
using Prometheus;

namespace Lachain.Networking.Hub
{
    public class ClientWorker : IDisposable
    {
        private static readonly ILogger<ClientWorker> Logger = LoggerFactory.GetLoggerForClass<ClientWorker>();

        private static readonly Counter MessageCounter = Metrics.CreateCounter(
            "lachain_hub_messages_sent_count",
            "Number of outgoing messages through communication hub",
            "peer", "message_type"
        );

        private static readonly Counter MessageBytesCounter = Metrics.CreateCounter(
            "lachain_hub_messages_sent_bytes",
            "Size of outgoing messages through communication hub",
            "peer", "message_type"
        );

        public byte[] PeerPublicKey { get; }
        private readonly IMessageFactory _messageFactory;
        private readonly HubConnector _hubConnector;
        private readonly Thread _worker;
        private bool _isConnected = false;
        private int _eraMsgCounter;
        private readonly object _messageReceived = new object();

        private readonly Queue<NetworkMessage> _nonPriorityMessageQueue = new Queue<NetworkMessage>();
        private readonly Queue<NetworkMessage> _priorityMessageQueue = new Queue<NetworkMessage>();

        public ClientWorker(ECDSAPublicKey peerPublicKey, IMessageFactory messageFactory, HubConnector hubConnector)
        {
            _messageFactory = messageFactory;
            _hubConnector = hubConnector;
            PeerPublicKey = peerPublicKey.EncodeCompressed();
            _worker = new Thread(Worker);
        }

        public ClientWorker(byte[] peerPublicKey, IMessageFactory messageFactory, HubConnector hubConnector)
        {
            _messageFactory = messageFactory;
            _hubConnector = hubConnector;
            PeerPublicKey = peerPublicKey;
            _worker = new Thread(Worker);
        }

        public void Start()
        {
            _eraMsgCounter = 0;
            _worker.Start();
        }

        public void Stop()
        {
            if (!_isConnected)
                return;
                
            _isConnected = false;
            _worker.Join();
        }


        public void AddMsgToQueue(NetworkMessage message, bool priorityMessage)
        {
            if (priorityMessage)
            {
                lock (_priorityMessageQueue)
                    _priorityMessageQueue.Enqueue(message);
            }
            else
            {
                lock (_nonPriorityMessageQueue)
                    _nonPriorityMessageQueue.Enqueue(message);
            }

            lock (_messageReceived)
                Monitor.PulseAll(_messageReceived);
        }

        private void Worker()
        {
            _isConnected = true;
            while (_isConnected)
            {

                try
                {
                    lock (_messageReceived)
                    {
                        while (_priorityMessageQueue.Count == 0 && _nonPriorityMessageQueue.Count == 0)
                            Monitor.Wait(_messageReceived);
                    }
                    
                    var now = TimeUtils.CurrentTimeMillis();
                    MessageBatchContent toSend = new MessageBatchContent();

                    const int maxSendSize = 64 * 1024; // let's not send more than 64 KiB at once
                    bool isConsensusMessage = false;
                    lock (_priorityMessageQueue)
                    {
                        while (_priorityMessageQueue.Count > 0 && toSend.CalculateSize() < maxSendSize)
                        {
                            var message = _priorityMessageQueue.Dequeue();
                            toSend.Messages.Add(message);
                            isConsensusMessage = true;
                        }
                    }

                    lock (_nonPriorityMessageQueue)
                    {
                        while (_nonPriorityMessageQueue.Count > 0 && toSend.CalculateSize() < maxSendSize)
                        {
                            var message = _nonPriorityMessageQueue.Dequeue();
                            toSend.Messages.Add(message);
                        }
                    }

                    if (toSend.Messages.Count > 0)
                    {
                        var megaBatch = _messageFactory.MessagesBatch(toSend.Messages);
                        var megaBatchBytes = megaBatch.ToByteArray();
                        if (megaBatchBytes.Length == 0)
                            throw new Exception("Cannot send empty message");
                        Logger.LogTrace(
                            $"Sending {toSend.Messages.Count} messages to hub, {megaBatchBytes.Length} bytes total, peer = {PeerPublicKey.ToHex()}"); 
                        var messageTypes = toSend.Messages.Select(m =>
                            m.MessageCase != NetworkMessage.MessageOneofCase.ConsensusMessage
                                ? m.MessageCase.ToString()
                                : m.ConsensusMessage.PrettyTypeString()
                        );
                        Logger.LogTrace($"Messages types: {string.Join("; ", messageTypes)}");
                        foreach (var message in toSend.Messages)
                        {
                            MessageCounter
                                .WithLabels(PeerPublicKey.ToHex(), message.MessageCase.ToString())
                                .Inc();
                            MessageBytesCounter
                                .WithLabels(PeerPublicKey.ToHex(), message.MessageCase.ToString())
                                .Inc(message.CalculateSize());
                        }

                        if (isConsensusMessage)
                            _hubConnector.Send(PeerPublicKey, megaBatchBytes);
                        else
                            _hubConnector.TrySend(PeerPublicKey, megaBatchBytes);
                        _eraMsgCounter += 1;
                    }
                    
                    var toSleep = Math.Clamp(250 - (long) (TimeUtils.CurrentTimeMillis() - now), 1, 1000);
                    Thread.Sleep(TimeSpan.FromMilliseconds(toSleep));
                }
                catch (Exception exception)
                {
                    Logger.LogError($"Failed to send messages: {exception}");
                }
            }
        }

        public void Dispose()
        {
            lock (_priorityMessageQueue)
                _priorityMessageQueue.Clear();
            lock (_nonPriorityMessageQueue)
                _nonPriorityMessageQueue.Clear();
            Stop();
        }

        public int AdvanceEra(ulong era)
        {
            var sentBatches = _eraMsgCounter;
            _eraMsgCounter = 0;
            Logger.LogTrace($"Sent {sentBatches} msgBatches during era #{era - 1} for {PeerPublicKey.ToHex()}");

            return sentBatches;
        }
    }
}