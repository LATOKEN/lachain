using System;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using Lachain.Logger;
using Lachain.Utility.Utils;

namespace Lachain.Networking.Hub
{
    public class HubConnector : IDisposable
    {
        private static readonly ILogger<HubConnector> Logger = LoggerFactory.GetLoggerForClass<HubConnector>();

        private bool _started;
        private bool _running;
        private readonly IMessageFactory _messageFactory;
        private readonly int _hubMetricsPort;

        private Thread? _readWorker;
        private readonly Thread _hubThread;

        public event EventHandler<byte[]>? OnMessage;

        public HubConnector(string hubBootstrapAddresses, int hubMetricsPort, IMessageFactory messageFactory, string? logLevel)
        {
            logLevel ??= Logger.LowestLogLevel().Name;
            CommunicationHub.Net.Hub.SetLogLevel($"<root>={logLevel.ToUpper()}");
            _hubThread = new Thread(() => CommunicationHub.Net.Hub.Start(hubBootstrapAddresses));
            _messageFactory = messageFactory;
            _hubMetricsPort = hubMetricsPort;
        }

        private byte[] RequestHubId()
        {
            while (true)
            {
                try
                {
                    var id = CommunicationHub.Net.Hub.GetKey();
                    if (id.Length > 0) return id;
                }
                catch (Exception e)
                {
                    Logger.LogWarning($"Hub is not yet available: {e.Message}");
                }

                Thread.Sleep(TimeSpan.FromMilliseconds(1_000));
            }
        }

        public void Start()
        {
            _started = true;
            _hubThread.Start();
            Logger.LogDebug("Requesting hub id from communication hub");
            var hubId = RequestHubId();
            Thread.Sleep(TimeSpan.FromMilliseconds(5_000));
            Logger.LogDebug("Sending init request to communication hub");
            var reply = CommunicationHub.Net.Hub.Init(_messageFactory.SignCommunicationHubInit(hubId), _hubMetricsPort);
            Logger.LogDebug($"init result: {reply}");
            if (!reply) Logger.LogError("Failed to start hub"); 
            Thread.Sleep(TimeSpan.FromMilliseconds(5_000));
            Logger.LogDebug("Establishing bi-directional connection with hub");
            _readWorker = new Thread(ReadWorker);
            _running = true;
            _readWorker.Start();
        }

        private void ReadWorker()
        {
            while (_running)
            {
                try
                {
                    var messages = CommunicationHub.Net.Hub.Get();
                    foreach (var message in messages)
                    {
                        try
                        {
                            OnMessage?.Invoke(this, CompressUtils.DeflateDecompress(message).ToArray());
                        }
                        catch (Exception e)
                        {
                            Logger.LogError($"Error occured: {e}");
                        }                        
                    }
                    if (messages.Length == 0) Thread.Sleep(TimeSpan.FromMilliseconds(100));
                }
                catch (Exception e)
                {
                    Logger.LogError($"Error occured: {e}");
                }
            }
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Send(byte[] publicKey, byte[] message)
        {
            CommunicationHub.Net.Hub.Send(publicKey, CompressUtils.DeflateCompress(message).ToArray());
        }

        public void Dispose()
        {
            _running = false;
            if (_started)
                CommunicationHub.Net.Hub.Stop();
            _started = false;
            _readWorker?.Join();
        }
    }
}