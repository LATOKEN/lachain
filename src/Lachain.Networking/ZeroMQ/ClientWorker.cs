using System;
using System.Collections.Generic;
using System.Threading;
using Google.Protobuf;
using NetMQ;
using NetMQ.Sockets;
using Lachain.Proto;
using Lachain.Logger;

namespace Lachain.Networking.ZeroMQ
{
    public class ClientWorker : IRemotePeer, IDisposable
    {
        private static readonly ILogger<ClientWorker> Logger = LoggerFactory.GetLoggerForClass<ClientWorker>();

        public bool IsConnected { get; private set; }
        public PeerAddress Address { get; }
        public ECDSAPublicKey? PublicKey { get; }
        public DateTime Connected { get; } = DateTime.Now;

        private readonly PushSocket _socket;
        private readonly Thread _worker;

        public ClientWorker(PeerAddress peerAddress, ECDSAPublicKey? publicKey)
        {
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

        public static void SendOnce(PeerAddress address, NetworkMessage message)
        {
            var endpoint = $"tcp://{address.Host}:{address.Port}";
            using var socket = new PushSocket();
            socket.Connect(endpoint);
            socket.SendFrame(message.ToByteArray());
            Thread.Sleep(TimeSpan.FromSeconds(1)); // TODO: wtf?
            socket.Disconnect(endpoint);
        }

        private readonly Queue<NetworkMessage> _messageQueue = new Queue<NetworkMessage>();
        private readonly object _queueNotEmpty = new object();

        public void Send(NetworkMessage message)
        {
            lock (_queueNotEmpty)
            {
                _messageQueue.Enqueue(message);
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
                NetworkMessage message;
                lock (_queueNotEmpty)
                {
                    while (_messageQueue.Count == 0)
                    {
                        Monitor.Wait(_queueNotEmpty);
                    }

                    message = _messageQueue.Dequeue();
                }

                if (message is null)
                    continue;
                _socket.SendFrame(message.ToByteArray());
            }

            _socket.Disconnect(endpoint);
        }

        public void Dispose()
        {
            Stop();
            _socket.Dispose();
        }
    }
}