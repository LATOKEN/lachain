using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Google.Protobuf;
using Phorkus.Crypto;
using Phorkus.Proto;
using Phorkus.Utility.Utils;

namespace Phorkus.Networking
{
    public class NetworkManager : INetworkManager, INetworkBroadcaster, INetworkContext
    {
        public event OnClientConnectedDelegate OnClientConnected;
        public event OnClientClosedDelegate OnClientClosed;

        public IDictionary<PeerAddress, IRemotePeer> ActivePeers
        {
            get
            {
                return _clientWorkers.Where(entry => entry.Value.Node != null && _authorizedKeys.Keys.Contains(entry.Value.Node.PublicKey))
                    .ToDictionary(entry => entry.Value.Address, entry => entry.Value as IRemotePeer);
            }
        }

        public Node LocalNode { get; set; }

        public IMessageFactory MessageFactory => _messageFactory;

        private readonly IDictionary<PeerAddress, ClientWorker> _clientWorkers =
            new Dictionary<PeerAddress, ClientWorker>();
        private readonly ConcurrentDictionary<PublicKey, bool> _authorizedKeys = new ConcurrentDictionary<PublicKey, bool>();
        private readonly ICrypto _crypto;

        private MessageFactory _messageFactory;
        private ServerWorker _serverWorker;
        private IMessageHandler _messageHandler;
        private NetworkConfig _networkConfig;

        public NetworkManager(ICrypto crypto)
        {
            _crypto = crypto;
        }

        public IRemotePeer GetPeerByPublicKey(PublicKey publicKey)
        {
            foreach (var worker in _clientWorkers)
            {
                if (worker.Value.Node is null)
                    continue;
                var pk = worker.Value.Node.PublicKey;
                if (pk.Equals(publicKey))
                    return worker.Value;
            }
            throw new Exception("Unable to resolve peer by public key");
        }

        public bool IsConnected(PeerAddress address)
        {
            return _clientWorkers.ContainsKey(address);
        }
        
        private readonly object _hasPeersToConnect = new object();
        
        private bool _SelfConnect(IPAddress ipAddress)
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            if (host.AddressList.Contains(ipAddress))
                return true;
            var networkInterfaces = NetworkInterface.GetAllNetworkInterfaces();
            foreach (var ni in networkInterfaces)
            {
                if (ni.NetworkInterfaceType != NetworkInterfaceType.Wireless80211 &&
                    ni.NetworkInterfaceType != NetworkInterfaceType.Ethernet)
                {
                    continue;
                }
                foreach (var ip in ni.GetIPProperties().UnicastAddresses)
                {
                    if (ip.Address.AddressFamily != AddressFamily.InterNetwork)
                        continue;
                    if (!ip.Address.Equals(ipAddress))
                        continue;
                    return true;
                }
            }
            return false;
        }
        
        public IRemotePeer Connect(PeerAddress address)
        {
            lock (_hasPeersToConnect)
            {
                if (_clientWorkers.TryGetValue(address, out var worker))
                    return worker;
                if (_SelfConnect(IPAddress.Parse(address.Host)) && _networkConfig.Port == address.Port)
                    return null;
                worker = new ClientWorker(address, null);
                _clientWorkers.Add(address, worker);
                Monitor.PulseAll(_hasPeersToConnect);
                return worker;
            }
        }

        private void _ConnectToNode(PeerAddress address)
        {
            if (!_clientWorkers.TryGetValue(address, out var remotePeer))
                return;
            remotePeer.OnOpen += (worker, endpoint) =>
            {
                lock (_clientWorkers)
                {
                    if (_clientWorkers.ContainsKey(address))
                        return;
                    OnClientConnected?.Invoke(worker);
                    _clientWorkers.Add(address, worker);
                }
            };
            remotePeer.OnClose += (worker, endpoint) =>
            {
                lock (_clientWorkers)
                {
                    if (!_clientWorkers.ContainsKey(address))
                        return;
                    OnClientClosed?.Invoke(worker);
                    _clientWorkers.Remove(address);
                }
            };
            remotePeer.OnError += (worker, message) =>
            {
                Console.Error.WriteLine("Error: " + message);
            };
            if (_authorizedKeys.Keys.Contains(address.PublicKey))
                return;
            remotePeer.Send(_messageFactory.HandshakeRequest(LocalNode));
            if (!remotePeer.IsConnected)
                remotePeer.Start();
        }

        public void Broadcast(NetworkMessage networkMessage)
        {
            foreach (var peer in ActivePeers)
            {
                peer.Value.Send(networkMessage);
            }
        }

        private void _HandleOpen(string message)
        {
        }

        private MessageEnvelope _BuildEnvelope(IMessage message, Signature signature)
        {
            if (signature is null)
                throw new ArgumentNullException(nameof(signature));
            var bytes = message.ToByteArray();
            /* TODO: "we can cache public key to avoid recovers" */
            var rawPublicKey = _crypto.RecoverSignature(bytes, signature.Buffer.ToByteArray());
            if (rawPublicKey == null)
                throw new Exception("Unable to recover public key from signature");
            var publicKey = new PublicKey
            {
                Buffer = ByteString.CopyFrom(rawPublicKey)
            };
            if (!_authorizedKeys.Keys.Contains(publicKey))
                throw new Exception("This node hasn't been authorized (" + rawPublicKey.ToHex() + ")");
            var envelope = new MessageEnvelope
            {
                MessageFactory = _messageFactory,
                PublicKey = publicKey,
                RemotePeer = GetPeerByPublicKey(publicKey),
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
                ? clientWorker
                : Connect(address);
            peer?.Send(_messageFactory.HandshakeReply(LocalNode));
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
            if (_authorizedKeys.Keys.Contains(publicKey))
                return;
            Console.WriteLine("Authorized: " + publicKey.Buffer.ToHex());
            _clientWorkers[PeerAddress.FromNode(reply.Node)].Node = reply.Node;
            _authorizedKeys.TryAdd(publicKey, true);
        }

        private void _HandleMessageUnsafe(NetworkMessage message)
        {
            switch (message.MessageCase)
            {
                case NetworkMessage.MessageOneofCase.HandshakeRequest:
                    _HandshakeRequest(message.Signature, message.HandshakeRequest);
                    break;
                case NetworkMessage.MessageOneofCase.HandshakeReply:
                    _HandshakeReply(message.Signature, message.HandshakeReply);
                    break;
                case NetworkMessage.MessageOneofCase.PingRequest:
                    _messageHandler.PingRequest(
                        _BuildEnvelope(message.PingRequest, message.Signature),
                        message.PingRequest);
                    break;
                case NetworkMessage.MessageOneofCase.PingReply:
                    _messageHandler.PingReply(
                        _BuildEnvelope(message.PingReply, message.Signature),
                        message.PingReply);
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
                case NetworkMessage.MessageOneofCase.ConsensusMessage:
                    _messageHandler.ConsensusMessage(
                        _BuildEnvelope(message.ConsensusMessage, message.Signature),
                        message.ConsensusMessage
                    );
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(message),
                        "Unable to resolve message type (" + message.MessageCase + ") from protobuf structure");
            }
        }

        private void _HandleMessage(byte[] buffer)
        {
            var message = NetworkMessage.Parser.ParseFrom(buffer);
            if (message is null)
                return;
            try
            {
                _HandleMessageUnsafe(message);
            }
            catch (Exception e)
            {
                Console.Error.WriteLine(e);
            }
        }

        private void _HandleClose(string message)
        {
        }

        private void _HandleError(string message)
        {
            Console.Error.WriteLine(message);
        }

        public void Start(NetworkConfig networkConfig, KeyPair keyPair, IMessageHandler messageHandler)
        {
            _messageHandler = messageHandler;
            _networkConfig = networkConfig ?? throw new ArgumentNullException(nameof(networkConfig));
            _messageFactory = new MessageFactory(keyPair, _crypto);
            _serverWorker = new ServerWorker(networkConfig);
            LocalNode = new Node
            {
                Version = 0,
                Timestamp = (ulong) DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Address = $"{networkConfig.MyHost}:{networkConfig.Port}",
                PublicKey = keyPair.PublicKey,
                Nonce = (uint) new Random().Next(1 << 30),
                BlockHeight = 0,
                Agent = "Phorkus-v0.0-dev"
            };
            _serverWorker.OnOpen += _HandleOpen;
            _serverWorker.OnMessage += _HandleMessage;
            _serverWorker.OnClose += _HandleClose;
            _serverWorker.OnError += _HandleError;
            _serverWorker.Start();
            Task.Factory.StartNew(_ConnectWorker, TaskCreationOptions.LongRunning);
            foreach (var peer in networkConfig.Peers)
                Connect(PeerAddress.Parse(peer));
        }

        private void _ConnectWorker()
        {
            var thread = Thread.CurrentThread;
            while (thread.IsAlive)
            {
                lock (_hasPeersToConnect)
                {
                    Monitor.Wait(_hasPeersToConnect, TimeSpan.FromSeconds(5));
                    if (!_clientWorkers.Any())
                        continue;
                    foreach (var entry in _clientWorkers)
                    {
                        try
                        {
                            _ConnectToNode(entry.Key);
                        }
                        catch (Exception e)
                        {
                            Console.Error.WriteLine(e);
                        }
                    }
                }
            }
        }

        public void Stop()
        {
            _serverWorker.Stop();
        }
    }
}