using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Google.Protobuf;
using Lachain.Crypto;
using Lachain.Logger;
using Lachain.Proto;
using Lachain.Utility.Utils;

namespace Lachain.Networking.Hub
{
    public class ClientWorker : IDisposable
    {
        private static readonly ILogger<ClientWorker> Logger = LoggerFactory.GetLoggerForClass<ClientWorker>();
        public byte[] PeerPublicKey { get; }
        private readonly IMessageFactory _messageFactory;
        private readonly HubConnector _hubConnector;
        private readonly Thread _worker;
        private bool _isConnected;
        private int _eraMsgCounter;

        private readonly LinkedList<NetworkMessage> _messageQueue = new LinkedList<NetworkMessage>();

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
            _isConnected = false;
            _worker.Join();
        }


        public void AddMsgToQueue(NetworkMessage message)
        {
            lock (_messageQueue)
            {
                if (message.MessageCase == NetworkMessage.MessageOneofCase.PingRequest &&
                    _messageQueue.Any(x => x.MessageCase == NetworkMessage.MessageOneofCase.PingRequest)
                ) return;
                _messageQueue.AddLast(message);
            }
        }

        private void Worker()
        {
            _isConnected = true;
            while (_isConnected)
            {
                var now = TimeUtils.CurrentTimeMillis();
                MessageBatchContent toSend = new MessageBatchContent();

                lock (_messageQueue)
                {
                    const int maxSendSize = 64 * 1024; // let's not send more than 64 KiB at once
                    while (_messageQueue.Count > 0 && toSend.CalculateSize() < maxSendSize)
                    {
                        var message = _messageQueue.First.Value;
                        toSend.Messages.Add(message);
                        _messageQueue.RemoveFirst();
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
                    _hubConnector.Send(PeerPublicKey, megaBatchBytes);
                    _eraMsgCounter += 1;
                }

                var toSleep = Math.Clamp(50 - (long) (TimeUtils.CurrentTimeMillis() - now), 1, 1000);
                Thread.Sleep(TimeSpan.FromMilliseconds(toSleep));
            }
        }

        public void Dispose()
        {
            lock (_messageQueue) _messageQueue.Clear();
            Stop();
        }

        public int AdvanceEra(long era)
        {
            var sentBatches = _eraMsgCounter;
            _eraMsgCounter = 0;
            Logger.LogTrace($"Sent {sentBatches} msgBatches during era #{era - 1} for {PeerPublicKey.ToHex()}");

            return sentBatches;
        }
    }
}