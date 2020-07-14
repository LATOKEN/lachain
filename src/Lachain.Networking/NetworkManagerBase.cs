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
using Lachain.Crypto;
using Lachain.Crypto.ECDSA;
using Lachain.Logger;
using Lachain.Networking.Consensus;
using Lachain.Networking.ZeroMQ;
using Lachain.Proto;
using Lachain.Utility.Utils;
using PingReply = Lachain.Proto.PingReply;

namespace Lachain.Networking
{
    public abstract class NetworkManagerBase : INetworkManager, INetworkContext, INetworkBroadcaster,
        IConsensusMessageDeliverer
    {
        private static readonly ICrypto Crypto = CryptoProvider.GetCrypto();

        private static readonly ILogger<NetworkManagerBase> Logger =
            LoggerFactory.GetLoggerForClass<NetworkManagerBase>();

        public IDictionary<PeerAddress, IRemotePeer> ActivePeers
        {
            get
            {
                return _clientWorkers
                    .ToDictionary(
                        entry => entry.Value.Address,
                        entry => entry.Value as IRemotePeer
                    );
            }
        }

        public Node LocalNode { get; }

        public IMessageFactory? MessageFactory => _messageFactory;

        private readonly IDictionary<PeerAddress, ClientWorker> _clientWorkers =
            new ConcurrentDictionary<PeerAddress, ClientWorker>();

        private readonly MessageFactory _messageFactory;

        private readonly ServerWorker _serverWorker;

        private readonly NetworkConfig _networkConfig;
        public ConsensusNetworkManager ConsensusNetworkManager { get; }
        public event EventHandler<(MessageEnvelope envelope, PingRequest message)>? OnPingRequest;
        public event EventHandler<(MessageEnvelope envelope, PingReply message)>? OnPingReply;

        public event EventHandler<(MessageEnvelope envelope, GetBlocksByHashesRequest message)>?
            OnGetBlocksByHashesRequest;

        public event EventHandler<(MessageEnvelope envelope, GetBlocksByHashesReply message)>? OnGetBlocksByHashesReply;

        public event EventHandler<(MessageEnvelope envelope, GetBlocksByHeightRangeRequest message)>?
            OnGetBlocksByHeightRangeRequest;

        public event EventHandler<(MessageEnvelope envelope, GetBlocksByHeightRangeReply message)>?
            OnGetBlocksByHeightRangeReply;

        public event EventHandler<(MessageEnvelope envelope, GetTransactionsByHashesRequest message)>?
            OnGetTransactionsByHashesRequest;

        public event EventHandler<(MessageEnvelope envelope, GetTransactionsByHashesReply message)>?
            OnGetTransactionsByHashesReply;

        public event EventHandler<(MessageEnvelope envelope, ConsensusMessage message)>? OnConsensusMessage;

        public NetworkManagerBase(NetworkConfig networkConfig, EcdsaKeyPair keyPair)
        {
            if (networkConfig?.Peers is null) throw new ArgumentNullException();
            _networkConfig = networkConfig;
            _messageFactory = new MessageFactory(keyPair);
            LocalNode = new Node
            {
                Version = 0,
                Timestamp = (ulong) DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Address = $"{networkConfig.MyHost}:{networkConfig.Port}",
                PublicKey = keyPair.PublicKey,
                Nonce = (uint) new Random().Next(1 << 30),
                BlockHeight = 0,
                Agent = "Lachain-v0.0-dev"
            };
            ConsensusNetworkManager = new ConsensusNetworkManager(_messageFactory, networkConfig, LocalNode);
            _serverWorker = new ServerWorker(networkConfig.Address, networkConfig.Port);
            _serverWorker.OnMessage += _HandleMessage;
            _serverWorker.OnError += HandleError;
            ConsensusNetworkManager.OnMessage += (sender, e) => OnConsensusMessage?.Invoke(sender, e);
        }

        public bool IsReady => _serverWorker != null && _serverWorker.IsActive;

        public void Start()
        {
            _serverWorker.Start();
            Task.Factory.StartNew(_ConnectWorker, TaskCreationOptions.LongRunning);
            foreach (var peer in _networkConfig.Peers!)
                Connect(PeerAddress.Parse(peer));
        }

        public IRemotePeer? GetPeerByPublicKey(ECDSAPublicKey publicKey)
        {
            return _clientWorkers.Values
                .FirstOrDefault(x => publicKey.Equals(x?.PublicKey));
        }

        public bool IsConnected(PeerAddress address)
        {
            return _clientWorkers.ContainsKey(address);
        }

        private static bool _IsSelfConnect(IPAddress ipAddress)
        {
            var localHost = new IPAddress(0x0100007f);
            if (ipAddress.Equals(localHost))
                return true;
            var host = Dns.GetHostEntry(Dns.GetHostName());
            if (host.AddressList.Contains(ipAddress))
                return true;
            var networkInterfaces = NetworkInterface.GetAllNetworkInterfaces();
            return networkInterfaces
                .Where(ni =>
                    ni.NetworkInterfaceType == NetworkInterfaceType.Wireless80211 ||
                    ni.NetworkInterfaceType == NetworkInterfaceType.Ethernet
                )
                .Any(ni => ni.GetIPProperties()
                    .UnicastAddresses.Where(ip => ip.Address.AddressFamily == AddressFamily.InterNetwork)
                    .Any(ip => ip.Address.Equals(ipAddress))
                );
        }

        private readonly object _hasPeersToConnect = new object();

        public IRemotePeer? Connect(PeerAddress address)
        {
            Logger.LogTrace($"Connecting to peer {address}");
            lock (_hasPeersToConnect)
            {
                if (_clientWorkers.TryGetValue(address, out var worker))
                    return worker;
                if (_IsSelfConnect(IPAddress.Parse(address.Host)) && _networkConfig.Port == address.Port)
                    return null;
                if (address.PublicKey is null)
                    throw new InvalidOperationException($"Cannot connect to peer {address}: no public key");
                worker = new ClientWorker(address, address.PublicKey!);
                _clientWorkers.Add(address, worker);
                worker.Start();
                Monitor.PulseAll(_hasPeersToConnect);
                Logger.LogTrace($"Successfully connected to peer {address}");
                return worker;
            }
        }

        private MessageEnvelope _BuildEnvelope(IMessage message, Signature signature)
        {
            var bytes = message.ToByteArray();
            var rawPublicKey = Crypto.RecoverSignature(bytes, signature.Encode());
            var publicKey = rawPublicKey.ToPublicKey();
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
            if (request.Node.PublicKey is null)
                throw new Exception("Public key can't be null");
            var isValid = Crypto.VerifySignature(
                request.ToByteArray(),
                signature.Encode(),
                request.Node.PublicKey.EncodeCompressed()
            );
            if (!isValid) throw new Exception("Unable to verify message using public key specified");
            var address = PeerAddress.FromNode(request.Node);
            var peer = _clientWorkers.TryGetValue(address, out var clientWorker)
                ? clientWorker
                : Connect(address);
            var port = ConsensusNetworkManager.GetReadyForConnect(request.Node.PublicKey);
            peer?.Send(_messageFactory.HandshakeReply(LocalNode, port));
        }

        private void _HandshakeReply(Signature signature, HandshakeReply reply)
        {
            var isValid = Crypto.VerifySignature(
                reply.ToByteArray(), signature.Encode(), reply.Node.PublicKey.EncodeCompressed()
            );
            if (!isValid) throw new Exception("Unable to verify message using public key specified");
            var address = PeerAddress.FromNode(reply.Node);
            address.Port = (int) reply.Port;
            ConsensusNetworkManager.InitOutgoingConnection(reply.Node.PublicKey, address);
        }

        private void _HandleMessage(object sender, byte[] buffer)
        {
            NetworkMessage? message = null;
            try
            {
                message = NetworkMessage.Parser.ParseFrom(buffer);
            }
            catch (Exception e)
            {
                Logger.LogError($"Unable to parse protocol message: {e}");
                Logger.LogTrace($"Original message bytes: {buffer.ToHex()}");
            }

            if (message is null)
            {
                Logger.LogError("Unable to parse protocol message");
                Logger.LogTrace($"Original message bytes: {buffer.ToHex()}");
                return;
            }

            try
            {
                HandleMessageUnsafe(message);
            }
            catch (Exception e)
            {
                Logger.LogError($"Unexpected error occurred: {e}");
            }
        }

        private static void HandleError(object sender, Exception error)
        {
            Logger.LogError($"Server error: {error}");
        }

        private void HandleMessageUnsafe(NetworkMessage message)
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
                    OnPingRequest?.Invoke(
                        this,
                        (_BuildEnvelope(message.PingRequest, message.Signature), message.PingRequest)
                    );
                    break;
                case NetworkMessage.MessageOneofCase.PingReply:
                    OnPingReply?.Invoke(
                        this,
                        (_BuildEnvelope(message.PingReply, message.Signature), message.PingReply)
                    );
                    break;
                case NetworkMessage.MessageOneofCase.GetBlocksByHashesRequest:
                    OnGetBlocksByHashesRequest?.Invoke(
                        this,
                        (_BuildEnvelope(message.GetBlocksByHashesRequest, message.Signature),
                            message.GetBlocksByHashesRequest)
                    );
                    break;
                case NetworkMessage.MessageOneofCase.GetBlocksByHashesReply:
                    OnGetBlocksByHashesReply?.Invoke(
                        this,
                        (_BuildEnvelope(message.GetBlocksByHashesReply, message.Signature),
                            message.GetBlocksByHashesReply)
                    );
                    break;
                case NetworkMessage.MessageOneofCase.GetBlocksByHeightRangeRequest:
                    OnGetBlocksByHeightRangeRequest?.Invoke(
                        this,
                        (_BuildEnvelope(message.GetBlocksByHeightRangeRequest, message.Signature),
                            message.GetBlocksByHeightRangeRequest)
                    );
                    break;
                case NetworkMessage.MessageOneofCase.GetBlocksByHeightRangeReply:
                    OnGetBlocksByHeightRangeReply?.Invoke(
                        this,
                        (_BuildEnvelope(message.GetBlocksByHeightRangeReply, message.Signature),
                            message.GetBlocksByHeightRangeReply)
                    );
                    break;
                case NetworkMessage.MessageOneofCase.GetTransactionsByHashesRequest:
                    OnGetTransactionsByHashesRequest?.Invoke(
                        this,
                        (_BuildEnvelope(message.GetTransactionsByHashesRequest, message.Signature),
                            message.GetTransactionsByHashesRequest)
                    );
                    break;
                case NetworkMessage.MessageOneofCase.GetTransactionsByHashesReply:
                    OnGetTransactionsByHashesReply?.Invoke(
                        this,
                        (_BuildEnvelope(message.GetTransactionsByHashesReply, message.Signature),
                            message.GetTransactionsByHashesReply)
                    );
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(message),
                        "Unable to resolve message type (" + message.MessageCase + ") from protobuf structure");
            }
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
                    foreach (var address in _clientWorkers.Keys)
                    {
                        try
                        {
                            ClientWorker.SendOnce(address, _messageFactory.HandshakeRequest(LocalNode));
                        }
                        catch (Exception e)
                        {
                            Logger.LogError($"Failed to connect to node {address}: {e}");
                        }
                    }
                }
            }
        }

        public void AdvanceEra(long era)
        {
            ConsensusNetworkManager.AdvanceEra(era);
        }

        public void BroadcastLocalTransaction(TransactionReceipt e)
        {
            var message = MessageFactory?.GetTransactionsByHashesReply(new[] {e}) ??
                          throw new InvalidOperationException();
            Broadcast(message);
        }

        public void Stop()
        {
            _serverWorker?.Stop();
        }

        public void Broadcast(NetworkMessage networkMessage)
        {
            foreach (var client in _clientWorkers.Values)
            {
                client.Send(networkMessage);
            }
        }

        public void SendTo(ECDSAPublicKey publicKey, NetworkMessage networkMessage)
        {
            ConsensusNetworkManager.SendTo(publicKey, networkMessage);
        }
    }
}