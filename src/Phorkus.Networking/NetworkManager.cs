using System;
using System.Collections.Generic;
using Google.Protobuf;
using Phorkus.Proto;

namespace Phorkus.Networking
{
    public class NetworkManager : IBroadcaster
    {
        public delegate void OnClientConnectedDelegate(IRemotePeer remotePeer);
        public event OnClientConnectedDelegate OnClientConnected;

        public delegate void OnClientClosedDelegate(IRemotePeer remotePeer);
        public event OnClientClosedDelegate OnClientClosed;
        
        private readonly ServerWorker _serverWorker;
        private readonly NetworkConfig _networkConfig;
        
        private readonly IList<ClientWorker> _clientWorkers
            = new List<ClientWorker>();

        private readonly IMessageHandler _messageHandler;
        
        public NetworkManager(NetworkConfig networkConfig, IMessageHandler messageHandler)
        {
            _messageHandler = messageHandler ?? throw new ArgumentNullException(nameof(messageHandler));
            if (networkConfig is null)
                throw new ArgumentNullException(nameof(networkConfig));
            _serverWorker = new ServerWorker(networkConfig);
            
            _serverWorker.OnOpen += _HandleOpen;
            _serverWorker.OnMessage += _HandleMessage;
            _serverWorker.OnClose += _HandleClose;
            _serverWorker.OnError += _HandleError;
        }
        
        public IRemotePeer Connect(PeerAddress address)
        {
            var client = new ClientWorker(address, null);
            client.OnOpen += (worker, endpoint) =>
            {
                OnClientConnected?.Invoke(worker);
                _clientWorkers.Add(worker);
            };
            client.OnClose += (worker, endpoint) =>
            {
                OnClientClosed?.Invoke(worker);
                _clientWorkers.Remove(worker);
            };
            client.Start();
            return client;
        }
        
        public void Broadcast<T>(IMessage<T> message)
            where T : IMessage<T>
        {
        }
        
        private void _HandleOpen(string message)
        {
        }
        
        private void _HandleMessage(byte[] buffer)
        {
            var message = NetworkMessage.Parser.ParseFrom(buffer);
            if (message is null)
                return;
            switch (message.MessageCase)
            {
                case NetworkMessage.MessageOneofCase.HandshakeRequest:
                    _messageHandler.HandshakeRequest(message.HandshakeRequest);
                    break;
                case NetworkMessage.MessageOneofCase.HandshakeReply:
                    _messageHandler.HandshakeReply(message.HandshakeReply);
                    break;
                case NetworkMessage.MessageOneofCase.GetBlocksByHashesRequest:
                    _messageHandler.GetBlocksByHashesRequest(message.GetBlocksByHashesRequest);
                    break;
                case NetworkMessage.MessageOneofCase.GetBlocksByHashesReply:
                    _messageHandler.GetBlocksByHashesReply(message.GetBlocksByHashesReply);
                    break;
                case NetworkMessage.MessageOneofCase.GetBlocksByHeightRangeRequest:
                    _messageHandler.GetBlocksByHeightRangeRequest(message.GetBlocksByHeightRangeRequest);
                    break;
                case NetworkMessage.MessageOneofCase.GetBlocksByHeightRangeReply:
                    _messageHandler.GetBlocksByHeightRangeReply(message.GetBlocksByHeightRangeReply);
                    break;
                case NetworkMessage.MessageOneofCase.GetTransactionsByHashesRequest:
                    _messageHandler.GetTransactionsByHashesRequest(message.GetTransactionsByHashesRequest);
                    break;
                case NetworkMessage.MessageOneofCase.GetTransactionsByHashesReply:
                    _messageHandler.GetTransactionsByHashesReply(message.GetTransactionsByHashesReply);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(buffer), "Unable to resolve message type (" + message.MessageCase + ") from protobuf structure");
            }
        }
        
        private void _HandleClose(string message)
        {
        }

        private void _HandleError(string message)
        {
        }
        
        public void Start()
        {
            _serverWorker.Start();
        }
        
        public void Stop()
        {
            _serverWorker.Stop();
        }
    }
}