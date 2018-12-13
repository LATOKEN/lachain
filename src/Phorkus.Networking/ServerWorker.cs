using System;
using System.Threading;
using System.Threading.Tasks;
using ZeroMQ;

namespace Phorkus.Networking
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
        
        private bool _isActive;
        
        public ServerWorker(NetworkConfig networkConfig)
        {
            _networkConfig = networkConfig ?? throw new ArgumentNullException(nameof(networkConfig));
        }

        private void _Worker()
        {
            using (var context = new ZContext())
            using (var socket = new ZSocket(context, ZSocketType.PAIR))
            {
                var endpoint = $"tcp://{_networkConfig.Address}:{_networkConfig.Port}";
                socket.Bind(endpoint);
                _isActive = true;
                OnOpen?.Invoke(endpoint);
                while (_isActive)
                {
                    var frame = socket.ReceiveFrame(out var error);
                    if (!Equals(error, ZError.None))
                    {
                        OnError?.Invoke("Unable to receive frame, got error (" + error + ")");
                        continue;
                    }
                    var buffer = frame.Read();
                    if (buffer == null || buffer.Length <= 0)
                        continue;
                    try
                    {
                        OnMessage?.Invoke(buffer);
                    }
                    catch (Exception e)
                    {
                        Console.Error.WriteLine(e);
                        OnError?.Invoke(e.Message);
                    }
                }
                OnClose?.Invoke(endpoint);
            }
        }
        
        public void Start()
        {
            if (_isActive)
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
            if (_isActive)
                throw new Exception("Server hasn't been started yet");
            _isActive = false;
        }
    }
}