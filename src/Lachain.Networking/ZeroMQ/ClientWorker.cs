using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Google.Protobuf;
using NetMQ;
using NetMQ.Sockets;
using Lachain.Proto;

namespace Lachain.Networking.ZeroMQ
{
    public class ClientWorker : IRemotePeer
    {
        public delegate void OpenDelegate(ClientWorker clientWorker, string endpoint);

        public event OpenDelegate? OnOpen;

        public delegate void MessageDelegate(ClientWorker clientWorker, NetworkMessage message);

        public event MessageDelegate? OnSent;

        public delegate void CloseDelegate(ClientWorker clientWorker, string endpoint);

        public event CloseDelegate? OnClose;

        public bool IsConnected { get; set; }
        public bool IsKnown { get; set; }
        public PeerAddress Address { get; }
        public Node? Node { get; set; }
        public DateTime Connected { get; } = DateTime.Now;

        public ClientWorker(PeerAddress peerAddress, Node? node)
        {
            Address = peerAddress;
            Node = node;
        }

        private readonly Queue<NetworkMessage> _messageQueue
            = new Queue<NetworkMessage>();

        private readonly object _queueNotEmpty = new object();
        private readonly object _workerClosed = new object();

        public void Send(NetworkMessage message)
        {
            lock (_queueNotEmpty)
            {
                _messageQueue.Enqueue(message);
                Monitor.PulseAll(_queueNotEmpty);
            }
        }

        private void _Worker()
        {
            using (var socket = new PushSocket())
            {
                var endpoint = $"tcp://{Address.Host}:{Address.Port}";
                socket.Connect(endpoint);
                IsConnected = true;
                OnOpen?.Invoke(this, endpoint);
                while (IsConnected)
                {
                    NetworkMessage message;
                    lock (_queueNotEmpty)
                    {
                        while (_messageQueue.Count == 0)
                            Monitor.Wait(_queueNotEmpty);
                        message = _messageQueue.Dequeue();
                    }

                    if (message is null)
                        continue;
                    if (socket.TrySendFrame(message.ToByteArray()))
                        OnSent?.Invoke(this, message);
                }

                socket.Disconnect(endpoint);
                OnClose?.Invoke(this, endpoint);
                lock (_workerClosed)
                {
                    IsConnected = false;
                    Monitor.PulseAll(_workerClosed);
                }
            }
        }

        public void Start()
        {
            if (IsConnected)
                throw new Exception("Client worker has already been started");
            Task.Factory.StartNew(() =>
            {
                try
                {
                    _Worker();
                }
                catch (Exception e)
                {
                    Console.Error.WriteLine(e);
                }
            }, TaskCreationOptions.LongRunning);
        }

        public void Stop()
        {
            lock (_workerClosed)
            {
                IsConnected = false;
                while (IsConnected)
                    Monitor.Wait(_workerClosed);
            }
        }
    }
}