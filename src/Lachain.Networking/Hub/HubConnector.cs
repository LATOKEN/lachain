using System;
using Grpc.Net.Client;
using Lachain.Proto;

namespace Lachain.Networking.Hub
{
    public class HubConnector : IDisposable
    {
        private readonly GrpcChannel _channel;
        private readonly Proto.CommunicationHub.CommunicationHubClient _client;
        
        public event EventHandler<byte[]>? OnMessage;
        public event EventHandler<Exception>? OnError;

        public HubConnector(string endpoint)
        {
            _channel = GrpcChannel.ForAddress(endpoint);
            _client = new Proto.CommunicationHub.CommunicationHubClient(_channel);
        }

        public void Dispose()
        {
            _channel.Dispose();
        }

        public void Send(ECDSAPublicKey publicKey, byte[] message)
        {
            throw new NotImplementedException();
        }
    }
}