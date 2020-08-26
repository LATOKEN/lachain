using System;
using System.Runtime.CompilerServices;
using System.Threading;
using Google.Protobuf;
using Grpc.Core;
using Lachain.Crypto;
using Lachain.Logger;
using Lachain.Proto;
using Protobuf;

namespace Lachain.Networking.Hub
{
    public class HubConnector : IDisposable
    {
        private static readonly ILogger<HubConnector> Logger = LoggerFactory.GetLoggerForClass<HubConnector>();
        
        private bool _running;
        private readonly byte[] _hubId;
        private readonly CommunicationHub.CommunicationHubClient _client;
        private readonly IMessageFactory _messageFactory;

        private AsyncDuplexStreamingCall<InboundMessage, OutboundMessage>? _hubStream;
        private Thread? _readWorker;

        public event EventHandler<byte[]>? OnMessage;

        public HubConnector(string endpoint, IMessageFactory messageFactory)
        {
            Logger.LogDebug("Requesting hub id from communication hub");
            _messageFactory = messageFactory;
            var channel = new Channel(endpoint, ChannelCredentials.Insecure);
            _client = new CommunicationHub.CommunicationHubClient(channel);
            var hubKey = _client.GetKey(new GetHubIdRequest(), Metadata.Empty);
            if (hubKey?.Id is null) throw new Exception("Cannot connect to hub");
            _hubId = hubKey.Id.ToByteArray();
        }

        public void Start()
        {
            Logger.LogDebug("Sending init request to communication hub");
            var init = new InitRequest
            {
                Signature = ByteString.CopyFrom(_messageFactory.SignCommunicationHubInit(_hubId))
            };
            _client.Init(init);
            Thread.Sleep(TimeSpan.FromMilliseconds(5_000));
            Logger.LogDebug("Establishing bi-directional connection with hub");
            _hubStream = _client.Communicate() ?? throw new Exception("Cannot establish connection to hub");
            _readWorker = new Thread(ReadWorker);
            _running = true;
            _readWorker.Start();
        }

        public void Dispose()
        {
            _running = false;
            _readWorker?.Join();
            _hubStream?.Dispose();
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
                    continue;
                }

                var message = _hubStream.ResponseStream.Current.Data.ToByteArray();
                OnMessage?.Invoke(this, message);
            }
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Send(ECDSAPublicKey publicKey, byte[] message)
        {
            if (_hubStream is null) throw new InvalidOperationException("HubConnector is not yet initialized");
            var request = new InboundMessage
            {
                Data = ByteString.CopyFrom(message),
                PublicKey = ByteString.CopyFrom(publicKey.EncodeCompressed())
            };
            
            var task = _hubStream.RequestStream.WriteAsync(request);
            task.Wait();
        }
    }
}