using System;
using System.Threading;
using System.Threading.Tasks;
using Lachain.Logger;
using NetMQ;
using NetMQ.Sockets;

namespace Lachain.Networking.ZeroMQ
{
    public class ServerWorker : IDisposable
    {
        private static readonly ILogger<ServerWorker> Logger =
            LoggerFactory.GetLoggerForClass<ServerWorker>();

        public event EventHandler<byte[]>? OnMessage;
        public event EventHandler<Exception>? OnError;

        public bool IsActive { get; set; }

        public readonly int Port;

        private readonly PullSocket _socket;
        private readonly Thread _worker;

        public ServerWorker(string bindAddress, int port = 0)
        {
            _socket = new PullSocket();
            if (port == 0)
            {
                Port = _socket.BindRandomPort($"tcp://{bindAddress}");
            }
            else
            {
                Port = port;
                _socket.Bind($"tcp://{bindAddress}:{port}");
            }
            Logger.LogTrace($"Bound endpoint: tcp://{bindAddress}:{Port}");

            IsActive = true;
            _worker = new Thread(Worker);
        }

        public void Start()
        {
            _worker.Start();
        }

        public void Stop()
        {
            IsActive = false;
            _worker.Interrupt();
            _worker.Join();
        }

        private void Worker()
        {
            while (IsActive)
            {
                try
                {
                    if (!_socket.TryReceiveFrameBytes(TimeSpan.FromSeconds(1), out var buffer))
                        continue;
                    if (buffer == null || buffer.Length <= 0)
                        continue;
                    Task.Factory.StartNew(() =>
                    {
                        try
                        {
                            OnMessage?.Invoke(this, buffer);
                        }
                        catch (Exception e)
                        {
                            OnError?.Invoke(this, e);
                        }
                    });
                }
                catch (Exception e)
                {
                    Logger.LogError($"Unexpected network error: {e}");
                    IsActive = false;
                }
            }
            Logger.LogDebug($"Server worker for {Port} is terminated");
        }

        public void Dispose()
        {
            Stop();
            _socket.Dispose();
        }
    }
}