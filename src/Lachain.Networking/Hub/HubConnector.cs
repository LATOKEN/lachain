using System;
using System.Threading;
using Google.Protobuf;
using Grpc.Core;
using Grpc.Net.Client;
using Lachain.Crypto;
using Lachain.Proto;

namespace Lachain.Networking.Hub
{
    public class HubConnector : IDisposable
    {
        private readonly GrpcChannel _channel;
        private readonly AsyncDuplexStreamingCall<SendMessageRequest, MessageResponse> _hubStream;
        private bool _running;
        private readonly Thread _readWorker;

        public event EventHandler<byte[]>? OnMessage;

        public HubConnector(string endpoint, MessageFactory messageFactory)
        {
            MessageFactory messageFactory1 = messageFactory;
            _channel = GrpcChannel.ForAddress(endpoint);
            CommunicationHub.CommunicationHubClient client = new CommunicationHub.CommunicationHubClient(_channel);
            var hubKey = client.GetKey(new GetKeyRequest(), Metadata.Empty);
            if (hubKey?.HubPublicKey is null) throw new Exception("Cannot connect to hub");
            var signature = messageFactory1.SignCommunicationHubInit(
                messageFactory1.GetPublicKey().EncodeCompressed(),
                hubKey.HubPublicKey.ToByteArray()
            );
            var init = new InitRequest
            {
                NodePublicKey = ByteString.CopyFrom(messageFactory1.GetPublicKey().EncodeCompressed()),
                Signature = ByteString.CopyFrom(signature)
            };
            client.Init(init);
            _hubStream = client.Communicate() ?? throw new Exception("Cannot establish connection to hub");
            _readWorker = new Thread(ReadWorker);
            _running = true;
            _readWorker.Start();
        }

        public void Dispose()
        {
            _running = false;
            _readWorker.Join();
            _hubStream.Dispose();
            _channel.Dispose();
        }

        private void ReadWorker()
        {
            while (_running)
            {
                var task = _hubStream.ResponseStream.MoveNext();
                task.Wait();
                if (!task.Result) break;
                OnMessage?.Invoke(this, _hubStream.ResponseStream.Current.Content.ToByteArray());
            }
        }

        public void Send(ECDSAPublicKey publicKey, byte[] message)
        {
            var request = new SendMessageRequest
            {
                Content = ByteString.CopyFrom(message),
                To = ByteString.CopyFrom(publicKey.EncodeCompressed())
            };

            _hubStream.RequestStream.WriteAsync(request);
        }
    }
}