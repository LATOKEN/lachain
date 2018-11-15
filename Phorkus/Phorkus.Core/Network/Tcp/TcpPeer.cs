using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Phorkus.Core.Cryptography;
using Phorkus.Core.Network.Proto;
using Phorkus.Core.Proto;

namespace Phorkus.Core.Network.Tcp
{
    public class TcpPeer : IPeer
    {
        private const int SocketOperationTimeout = 300_000;
        
        private readonly NetworkStream _stream;
        private readonly Socket _socket;
        private readonly Queue<Message> _messages;
        private readonly ITransport _transport;

        private readonly object _queueNotEmpty = new object();

        public event EventHandler OnDisconnect;

        public bool IsConnected => _socket.Connected;

        public BloomFilter BloomFilter { get; set; }
        public IpEndPoint EndPoint { get; }
        public Node Node { get; set; }
        public bool IsReady { get; set; }
        public DateTime Connected { get; } = DateTime.Now;

        internal TcpPeer(Socket socket, ITransport transport)
        {
            _socket = socket ?? throw new ArgumentNullException(nameof(socket));
            _transport = transport ?? throw new ArgumentNullException(nameof(transport));

            _messages = new Queue<Message>();
            _stream = new NetworkStream(socket, true);

            IPEndPoint endPoint;
            if (socket.IsBound)
                endPoint = (IPEndPoint) socket.RemoteEndPoint;
            else
                endPoint = (IPEndPoint) socket.LocalEndPoint;
            
            EndPoint = new IpEndPoint
            {
                Protocol = Protocol.Tcp,
                Host = endPoint.Address.ToString(),
                Port = endPoint.Port
            };
        }

        public void Send(Message message)
        {
            lock (_queueNotEmpty)
            {
                _messages.Enqueue(message);
                if (_messages.Count == 0)
                    return;
                Monitor.PulseAll(_queueNotEmpty);
            }
        }

        public void Run()
        {
            while (IsConnected)
            {
                var messages = new Queue<Message>();
                lock (_queueNotEmpty)
                {
                    while (_messages.Count == 0)
                        Monitor.Wait(_queueNotEmpty, TimeSpan.FromSeconds(1));
                    messages = Interlocked.Exchange(ref messages, _messages);
                }

                using (var cancellationTokenSource = new CancellationTokenSource(SocketOperationTimeout))
                    _transport.WriteMessages(messages, _stream, cancellationTokenSource.Token);
            }
        }

        public Message Receive()
        {
            if (!IsConnected)
                return null;
            
            using (var tokenSource = new CancellationTokenSource(SocketOperationTimeout))
            {
                tokenSource.Token.Register(Disconnect);
                try
                {
                    return _transport.ReadMessage(_stream, tokenSource.Token);
                }
                catch (Exception error)
                {
                    Console.Error.WriteLine(error);
                }

                Disconnect();
            }

            return null;
        }

        public void Disconnect()
        {
            try
            {
                _socket.Shutdown(SocketShutdown.Both);
                _stream.Dispose();
                _socket.Dispose();
            }
            catch
            {
                // ignored
            }
        }
    }
}