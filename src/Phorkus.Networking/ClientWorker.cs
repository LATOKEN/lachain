using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Google.Protobuf;
using Phorkus.Proto;
using ZeroMQ;

namespace Phorkus.Networking
{
    public class ClientWorker : IRemotePeer
    {
        public delegate void OnOpenDelegate(ClientWorker clientWorker, string endpoint);
        public event OnOpenDelegate OnOpen;

        public delegate void OnMessageDelegate(ClientWorker clientWorker, IMessage message);
        public event OnMessageDelegate OnSent;
        
        public delegate void OnCloseDelegate(ClientWorker clientWorker, string endpoint);
        public event OnCloseDelegate OnClose;

        public delegate void OnErrorDelegate(ClientWorker clientWorker, string message);
        public event OnErrorDelegate OnError;

        public bool IsConnected { get; set; }
        public bool IsKnown { get; set; }
        public PeerAddress Address { get; }
        public Node Node { get; set; }
        public DateTime Connected { get; } = DateTime.Now;
        
        public ClientWorker(PeerAddress peerAddress, Node node)
        {
            Address = peerAddress;
            Node = node;
        }
        
        private readonly Queue<IMessage> _messageQueue
            = new Queue<IMessage>();
        
        private readonly object _queueNotEmpty = new object();
        private readonly object _shouldClose = new object();

        public void Send<T>(IMessage<T> message)
            where T : IMessage<T>
        {
            lock (_queueNotEmpty)
            {
                _messageQueue.Enqueue(message);
                Monitor.PulseAll(_queueNotEmpty);
            }
        }

        private void _Worker()
        {
            using (var context = new ZContext())
            using (var socket = new ZSocket(context, ZSocketType.REQ))
            {
                var endpoint = $"tcp://{Address.Host}:{Address.Port}";
                socket.Connect(endpoint);
                IsConnected = true;
                OnOpen?.Invoke(this, endpoint);
                while (IsConnected)
                {
                    IMessage message;
                    lock (_queueNotEmpty)
                    {
                        while (_messageQueue.Count == 0)
                            Monitor.Wait(_queueNotEmpty);
                        message = _messageQueue.Peek();
                    }
                    if (message is null)
                        continue;
                    socket.SendFrame(new ZFrame(message.ToByteArray()), out var error);
                    if (Equals(error, ZError.None))
                    {
                        OnSent?.Invoke(this, message);
                        continue;
                    }
                    OnError?.Invoke(this, $"Unable to send message, got error ({error})");
                }
                socket.Disconnect(endpoint);
                OnClose?.Invoke(this, endpoint);
            }
        }

        public void Start()
        {
            if (IsConnected)
                throw new Exception("Client has already been started");
            
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
            lock (_shouldClose)
            {
                IsConnected = false;
                Monitor.PulseAll(_shouldClose);
            }
        }
    }
}