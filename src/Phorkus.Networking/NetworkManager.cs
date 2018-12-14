using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Google.Protobuf;
using Phorkus.Crypto;
using Phorkus.Proto;
using Phorkus.Utility.Utils;

namespace Phorkus.Networking
{
    public class NetworkManager : INetworkManager, IBroadcaster, INetworkContext
    {
        public event OnClientConnectedDelegate OnClientConnected;
        public event OnClientClosedDelegate OnClientClosed;
        
        public ConcurrentDictionary<PeerAddress, IRemotePeer> ActivePeers { get; }
        public Node LocalNode { get; }

        private readonly IDictionary<PeerAddress, ClientWorker> _clientWorkers =
            new Dictionary<PeerAddress, ClientWorker>();

        private readonly IDictionary<PublicKey, Node> _activeNodes = new Dictionary<PublicKey, Node>();

        private readonly IDictionary<PublicKey, IRemotePeer> _publicKeyToRemotePeer =
            new Dictionary<PublicKey, IRemotePeer>();

        private readonly ISet<PublicKey> _authorizedKeys = new HashSet<PublicKey>();

        private readonly IMessageHandler _messageHandler;
        private readonly ServerWorker _serverWorker;
        private readonly MessageFactory _messageFactory;
        private readonly ICrypto _crypto;

        public NetworkManager(
            NetworkConfig networkConfig,
            IMessageHandler messageHandler,
            ICrypto crypto,
            KeyPair keyPair)
        {
            _messageHandler = messageHandler ?? throw new ArgumentNullException(nameof(messageHandler));
            _crypto = crypto;
            if (networkConfig is null)
                throw new ArgumentNullException(nameof(networkConfig));
            _messageFactory = new MessageFactory(keyPair, crypto);
            _serverWorker = new ServerWorker(networkConfig);

            _serverWorker.OnOpen += _HandleOpen;
            _serverWorker.OnMessage += _HandleMessage;
            _serverWorker.OnClose += _HandleClose;
            _serverWorker.OnError += _HandleError;

            LocalNode = new Node
            {
                Version = 0,
                Timestamp = (ulong) DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Address = $"tcp://192.168.88.154:{networkConfig.Port}",
                PublicKey = keyPair.PublicKey,
                Nonce = (uint) new Random().Next(1 << 30),
                BlockHeight = 0,
                Agent = "Phorkus-v0.0-dev"
            };
        }

        public IRemotePeer GetPeerByPublicKey(PublicKey publicKey)
        {
            return _publicKeyToRemotePeer.TryGetValue(publicKey, out var peer) ? peer : null;
        }

        public bool IsConnected(PeerAddress address)
        {
            return _clientWorkers.ContainsKey(address);
        }

        public IRemotePeer Connect(PeerAddress address)
        {
            if (_clientWorkers.TryGetValue(address, out var peer))
                return peer;
            if (_publicKeyToRemotePeer.TryGetValue(address.PublicKey, out var clientWorker))
                return clientWorker;
            var client = new ClientWorker(address, null);
            client.OnOpen += (worker, endpoint) =>
            {
                lock (_clientWorkers)
                {
                    if (_clientWorkers.ContainsKey(address))
                        return;
                    OnClientConnected?.Invoke(worker);
                    _publicKeyToRemotePeer.Add(address.PublicKey, worker);
                    _clientWorkers.Add(address, worker);
                    worker.Send(_messageFactory.HandshakeRequest(LocalNode));
                }
            };
            client.OnClose += (worker, endpoint) =>
            {
                lock (_clientWorkers)
                {
                    if (!_clientWorkers.ContainsKey(address))
                        return;
                    OnClientClosed?.Invoke(worker);
                    _publicKeyToRemotePeer.Remove(address.PublicKey);
                    _clientWorkers.Remove(address);
                }
            };
            client.OnError += (worker, message) => { Console.Error.WriteLine("Error: " + message); };
            client.Start();
            return client;
        }

        public void Broadcast<T>(IMessage<T> message)
            where T : IMessage<T>
        {
            throw new NotImplementedException();
        }

        private void _HandleOpen(string message)
        {
        }

        private MessageEnvelope _BuildEnvelope(IMessage message, Signature signature)
        {
            if (signature is null)
                throw new ArgumentNullException(nameof(signature));
            var bytes = message.ToByteArray();
            var rawPublicKey = _crypto.RecoverSignature(bytes, signature.Buffer.ToByteArray());
            if (rawPublicKey == null)
                throw new Exception("Unable to recover public key from signature");
            var publicKey = new PublicKey
            {
                Buffer = ByteString.CopyFrom(rawPublicKey)
            };
            if (!_authorizedKeys.Contains(publicKey))
                throw new Exception("This node hasn't been authorized");
            if (!_publicKeyToRemotePeer.TryGetValue(publicKey, out var remotePeer))
                throw new Exception("Unable to resolve remote peer by public key");
            var envelope = new MessageEnvelope
            {
                MessageFactory = _messageFactory,
                PublicKey = publicKey,
                RemotePeer = remotePeer,
                Signature = signature
            };
            return envelope;
        }

        private void _HandshakeRequest(Signature signature, HandshakeRequest request)
        {
            if (signature is null)
                throw new ArgumentNullException(nameof(signature));
            if (request.Node.PublicKey is null)
                throw new Exception("Public key can't be null");
            var isValid = _crypto.VerifySignature(request.ToByteArray(), signature.Buffer.ToByteArray(),
                request.Node.PublicKey.Buffer.ToByteArray());
            if (!isValid)
                throw new Exception("Unable to verify message using public key specified");
            var address = PeerAddress.FromNode(request.Node);
            var peer = _clientWorkers.TryGetValue(address, out var clientWorker)
                ? clientWorker : Connect(address);
            peer.Send(_messageFactory.HandshakeReply(LocalNode));            
        }

        private void _HandshakeReply(Signature signature, HandshakeReply reply)
        {
            if (signature is null)
                throw new ArgumentNullException(nameof(signature));
            var isValid = _crypto.VerifySignature(reply.ToByteArray(), signature.Buffer.ToByteArray(),
                reply.Node.PublicKey.Buffer.ToByteArray());
            if (!isValid)
                throw new Exception("Unable to verify message using public key specified");
            var publicKey = reply.Node.PublicKey;
            if (_authorizedKeys.Contains(publicKey))
                return;
            Console.WriteLine("Authorized: " + publicKey.Buffer.ToHex());
            _authorizedKeys.Add(publicKey);
        }

        private void _HandleMessage(byte[] buffer)
        {
            var message = NetworkMessage.Parser.ParseFrom(buffer);
            if (message is null)
                return;
            switch (message.MessageCase)
            {
                case NetworkMessage.MessageOneofCase.HandshakeRequest:
                    _HandshakeRequest(message.Signature, message.HandshakeRequest);
                    break;
                case NetworkMessage.MessageOneofCase.HandshakeReply:
                    _HandshakeReply(message.Signature, message.HandshakeReply);
                    break;
                case NetworkMessage.MessageOneofCase.GetBlocksByHashesRequest:
                    _messageHandler.GetBlocksByHashesRequest(
                        _BuildEnvelope(message.GetBlocksByHashesRequest, message.Signature),
                        message.GetBlocksByHashesRequest);
                    break;
                case NetworkMessage.MessageOneofCase.GetBlocksByHashesReply:
                    _messageHandler.GetBlocksByHashesReply(
                        _BuildEnvelope(message.GetBlocksByHashesReply, message.Signature),
                        message.GetBlocksByHashesReply);
                    break;
                case NetworkMessage.MessageOneofCase.GetBlocksByHeightRangeRequest:
                    _messageHandler.GetBlocksByHeightRangeRequest(
                        _BuildEnvelope(message.GetBlocksByHeightRangeRequest, message.Signature),
                        message.GetBlocksByHeightRangeRequest);
                    break;
                case NetworkMessage.MessageOneofCase.GetBlocksByHeightRangeReply:
                    _messageHandler.GetBlocksByHeightRangeReply(
                        _BuildEnvelope(message.GetBlocksByHeightRangeReply, message.Signature),
                        message.GetBlocksByHeightRangeReply);
                    break;
                case NetworkMessage.MessageOneofCase.GetTransactionsByHashesRequest:
                    _messageHandler.GetTransactionsByHashesRequest(
                        _BuildEnvelope(message.GetTransactionsByHashesRequest, message.Signature),
                        message.GetTransactionsByHashesRequest);
                    break;
                case NetworkMessage.MessageOneofCase.GetTransactionsByHashesReply:
                    _messageHandler.GetTransactionsByHashesReply(
                        _BuildEnvelope(message.GetTransactionsByHashesReply, message.Signature),
                        message.GetTransactionsByHashesReply);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(buffer),
                        "Unable to resolve message type (" + message.MessageCase + ") from protobuf structure");
            }
        }

        private void _HandleClose(string message)
        {
        }

        private void _HandleError(string message)
        {
            Console.Error.WriteLine(message);
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