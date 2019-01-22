using System;
using System.Threading.Tasks;
using NetMQ;
using NetMQ.Sockets;

namespace Phorkus.Networking.ZeroMQ
{
    public class ServerWorker
    {
        public delegate void OnOpenDelegate(string endpoint);
        public event OnOpenDelegate OnOpen;

        public delegate void OnMessageDelegate(byte[] message);
        public event OnMessageDelegate OnMessage;
        
        public delegate void OnCloseDelegate(string endpoint);
        public event OnCloseDelegate OnClose;

        public delegate void OnErrorDelegate(string message);
        public event OnErrorDelegate OnError;
        
        private readonly NetworkConfig _networkConfig;

        public bool IsActive { get; set; }

        public ServerWorker(NetworkConfig networkConfig)
        {
            _networkConfig = networkConfig ?? throw new ArgumentNullException(nameof(networkConfig));
        }

        private void _Worker()
        {
            var endpoint = $"tcp://{_networkConfig.Address}:{_networkConfig.Port}";
            using (var socket = new PullSocket())
            {
                socket.Bind(endpoint);
                IsActive = true;
                OnOpen?.Invoke(endpoint);
                while (IsActive)
                {
                    if (!socket.TryReceiveFrameBytes(out var buffer))
                        continue;
                    if (buffer == null || buffer.Length <= 0)
                        continue;
                    Task.Factory.StartNew(() =>
                    {
                        try
                        {
                            OnMessage?.Invoke(buffer);
                        }
                        catch (Exception e)
                        {
                            OnError?.Invoke(e.Message);
                        }
                    });
                }
                OnClose?.Invoke(endpoint);
                IsActive = false;
            }
        }
        
        public void Start()
        {
            if (IsActive)
                throw new Exception("Server has already been started");
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
            if (IsActive)
                throw new Exception("Server hasn't been started yet");
            IsActive = false;
        }
    }
}