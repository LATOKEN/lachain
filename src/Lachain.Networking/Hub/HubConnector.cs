using System;
using System.Runtime.CompilerServices;
using System.Threading;
using Google.Protobuf;
using Grpc.Core;
using Lachain.Crypto;
using Lachain.Logger;
using Lachain.Proto;

namespace Lachain.Networking.Hub
{
    public class HubConnector : IDisposable
    {
        private static readonly ILogger<HubConnector> Logger = LoggerFactory.GetLoggerForClass<HubConnector>();

        private bool _started;
        private bool _running;
        private readonly Proto.CommunicationHub.CommunicationHubClient _client;
        private readonly IMessageFactory _messageFactory;

        private AsyncDuplexStreamingCall<InboundMessage, OutboundMessage>? _hubStream;
        private readonly object _hubStreamLock = new object();
        private Thread? _readWorker;
        private readonly Thread _hubThread;

        public event EventHandler<byte[]>? OnMessage;

        public HubConnector(string grpcEndpoint, string hubBootstrapAddress, IMessageFactory messageFactory)
        {
            CommunicationHub.Net.Hub.SetLogLevel($"<root>={Logger.LowestLogLevel().Name.ToUpper()}");
            _hubThread = new Thread(() => CommunicationHub.Net.Hub.Start(grpcEndpoint, hubBootstrapAddress));
            _messageFactory = messageFactory;
            var channel = new Channel(grpcEndpoint, ChannelCredentials.Insecure);
            _client = new Proto.CommunicationHub.CommunicationHubClient(channel);
        }

        private byte[] RequestHubId()
        {
            while (true)
            {
                try
                {
                    var hubKey = _client.GetKey(new GetHubIdRequest(), Metadata.Empty);
                    return hubKey.Id.ToByteArray();
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
            Logger.LogDebug("Sending init request to communication hub");
            var init = new InitRequest
            {
                Signature = ByteString.CopyFrom(_messageFactory.SignCommunicationHubInit(hubId))
            };
            var reply = _client.Init(init);
            Console.WriteLine($"init result: {reply.Result}");
            Thread.Sleep(TimeSpan.FromMilliseconds(5_000));
            Logger.LogDebug("Establishing bi-directional connection with hub");
            _hubStream = _client.Communicate() ?? throw new Exception("Cannot establish connection to hub");
            _readWorker = new Thread(ReadWorker);
            _running = true;
            _readWorker.Start();
        }

        private void ReadWorker()
        {
            while (_running)
            {
                var task = _hubStream!.ResponseStream.MoveNext();
                task.Wait();
                if (!task.Result)
                {
                    _hubStream = _client.Communicate() ?? throw new Exception("Cannot establish connection to hub");
                    lock (_hubStreamLock) Monitor.Pulse(_hubStreamLock);
                    continue;
                }

                var message = _hubStream.ResponseStream.Current.Data.ToByteArray();
                if (message.Length > 0)
                    OnMessage?.Invoke(this, message);
                else
                    Logger.LogWarning("Received empty message from hub");
            }
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Send(ECDSAPublicKey publicKey, byte[] message)
        {
            lock (_hubStreamLock)
                while (_hubStream is null)
                    Monitor.Wait(_hubStreamLock);

            var request = new InboundMessage
            {
                Data = ByteString.CopyFrom(message),
                PublicKey = ByteString.CopyFrom(publicKey.EncodeCompressed())
            };

            var task = _hubStream.RequestStream.WriteAsync(request);
            task.Wait();
        }

        public void Dispose()
        {
            if (_started) CommunicationHub.Net.Hub.Stop();
            _running = false;
            _readWorker?.Join();
            _hubStream?.Dispose();
        }
    }
}