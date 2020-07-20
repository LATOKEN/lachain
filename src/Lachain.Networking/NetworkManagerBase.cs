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

        public event EventHandler<(PingRequest message, Action<PingReply> callback)>? OnPingRequest;
        public event EventHandler<(PingReply message, ECDSAPublicKey publicKey)>? OnPingReply;

        public event EventHandler<(GetBlocksByHashesRequest message, Action<GetBlocksByHashesReply> callback)>?
            OnGetBlocksByHashesRequest;

        public event EventHandler<(GetBlocksByHashesReply message, ECDSAPublicKey address)>? OnGetBlocksByHashesReply;

        public event EventHandler<(GetBlocksByHeightRangeRequest message, Action<GetBlocksByHeightRangeReply> callback)>
            ?
            OnGetBlocksByHeightRangeRequest;

        public event EventHandler<(GetBlocksByHeightRangeReply message, Action<GetBlocksByHashesRequest> callback)>?
            OnGetBlocksByHeightRangeReply;

        public event EventHandler<(GetTransactionsByHashesRequest message, Action<GetTransactionsByHashesReply> callback
                )>?
            OnGetTransactionsByHashesRequest;

        public event EventHandler<(GetTransactionsByHashesReply message, ECDSAPublicKey address)>?
            OnGetTransactionsByHashesReply;

        public event EventHandler<(ConsensusMessage message, ECDSAPublicKey publicKey)>? OnConsensusMessage;

        public IEnumerable<PeerAddress> GetConnectedPeers()
        {
            return _clientWorkers.Keys;
        }

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
            _serverWorker.OnError += (sender, error) => Logger.LogError($"Server error: {error}");
            ConsensusNetworkManager.OnMessage += (sender, e) => OnConsensusMessage?.Invoke(sender, e);
        }

        public void SendToPeerByPublicKey(ECDSAPublicKey publicKey, NetworkMessage message)
        {
            GetPeerByPublicKey(publicKey)?.Send(message);
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
                worker = new ClientWorker(address, address.PublicKey!, _messageFactory);
                _clientWorkers.Add(address, worker);
                worker.Start();
                Monitor.PulseAll(_hasPeersToConnect);
                Logger.LogTrace($"Successfully connected to peer {address}");
                return worker;
            }
        }

        private void _HandshakeRequest(HandshakeRequest request)
        {
            if (request.Node.PublicKey is null)
                throw new Exception("Public key can't be null");
            var address = PeerAddress.FromNode(request.Node);
            var peer = _clientWorkers.TryGetValue(address, out var clientWorker)
                ? clientWorker
                : Connect(address);
            var port = ConsensusNetworkManager.GetReadyForConnect(request.Node.PublicKey);
            peer?.Send(_messageFactory.HandshakeReply(LocalNode, port));
        }

        private void _HandshakeReply(HandshakeReply reply)
        {
            var address = PeerAddress.FromNode(reply.Node);
            address.Port = (int) reply.Port;
            ConsensusNetworkManager.InitOutgoingConnection(reply.Node.PublicKey, address);
        }

        private void _HandleMessage(object sender, byte[] buffer)
        {
            MessageBatch? batch;
            try
            {
                batch = MessageBatch.Parser.ParseFrom(buffer);
            }
            catch (Exception e)
            {
                Logger.LogError($"Unable to parse protocol message: {e}");
                Logger.LogTrace($"Original message bytes: {buffer.ToHex()}");
                return;
            }

            if (batch is null)
            {
                Logger.LogError("Unable to parse protocol message");
                Logger.LogTrace($"Original message bytes: {buffer.ToHex()}");
                return;
            }

            var publicKey = Crypto.RecoverSignature(batch.Content.ToArray(), batch.Signature.Encode())
                .ToPublicKey();
            var envelope = new MessageEnvelope
            {
                MessageFactory = _messageFactory,
                PublicKey = publicKey,
                RemotePeer = GetPeerByPublicKey(publicKey)
            };
            if (envelope.RemotePeer is null)
            {
                Logger.LogWarning(
                    $"Message from unrecognized peer {publicKey.ToHex()} or invalid signature, skipping");
                //: {string.Join(", ", batch.Content.Messages.Select(x => x.MessageCase.ToString()))}
                return;
            }

            Logger.LogTrace($"Envelope: pub={publicKey.ToHex()} peer={envelope.RemotePeer?.Address}");

            var messages = MessageBatchContent.Parser.ParseFrom(batch.Content);
            foreach (var message in messages.Messages)
            {
                try
                {
                    HandleMessageUnsafe(message, envelope);
                }
                catch (Exception e)
                {
                    Logger.LogError($"Unexpected error occurred: {e}");
                }
            }
        }

        private Action<IMessage> SendTo(IRemotePeer? peer)
        {
            return x =>
            {
                Logger.LogTrace($"Sending {x.GetType()} to {peer?.Address}");
                NetworkMessage msg = x switch
                {
                    PingReply pingReply => new NetworkMessage {PingReply = pingReply},
                    GetBlocksByHashesReply getBlocksByHashesReply => new NetworkMessage
                    {
                        GetBlocksByHashesReply = getBlocksByHashesReply
                    },
                    GetBlocksByHeightRangeReply getBlocksByHeightRangeReply => new NetworkMessage
                    {
                        GetBlocksByHeightRangeReply = getBlocksByHeightRangeReply
                    },
                    GetBlocksByHashesRequest getBlocksByHashesRequest => new NetworkMessage
                    {
                        GetBlocksByHashesRequest = getBlocksByHashesRequest
                    },
                    GetTransactionsByHashesReply getTransactionsByHashesReply => new NetworkMessage
                    {
                        GetTransactionsByHashesReply = getTransactionsByHashesReply
                    },
                    _ => throw new InvalidOperationException()
                };
                peer?.Send(msg);
            };
        }

        private void HandleMessageUnsafe(NetworkMessage message, MessageEnvelope envelope)
        {
            if (envelope.PublicKey is null) throw new InvalidOperationException();
            switch (message.MessageCase)
            {
                case NetworkMessage.MessageOneofCase.HandshakeRequest:
                    _HandshakeRequest(message.HandshakeRequest);
                    break;
                case NetworkMessage.MessageOneofCase.HandshakeReply:
                    _HandshakeReply(message.HandshakeReply);
                    break;
                case NetworkMessage.MessageOneofCase.PingRequest:
                    OnPingRequest?.Invoke(this, (message.PingRequest, SendTo(envelope.RemotePeer)));
                    break;
                case NetworkMessage.MessageOneofCase.PingReply:
                    OnPingReply?.Invoke(this, (message.PingReply, envelope.PublicKey));
                    break;
                case NetworkMessage.MessageOneofCase.GetBlocksByHashesRequest:
                    OnGetBlocksByHashesRequest?.Invoke(this,
                        (message.GetBlocksByHashesRequest, SendTo(envelope.RemotePeer))
                    );
                    break;
                case NetworkMessage.MessageOneofCase.GetBlocksByHashesReply:
                    OnGetBlocksByHashesReply?.Invoke(this, (message.GetBlocksByHashesReply, envelope.PublicKey));
                    break;
                case NetworkMessage.MessageOneofCase.GetBlocksByHeightRangeRequest:
                    OnGetBlocksByHeightRangeRequest?.Invoke(this,
                        (message.GetBlocksByHeightRangeRequest, SendTo(envelope.RemotePeer))
                    );
                    break;
                case NetworkMessage.MessageOneofCase.GetBlocksByHeightRangeReply:
                    OnGetBlocksByHeightRangeReply?.Invoke(this,
                        (message.GetBlocksByHeightRangeReply, SendTo(envelope.RemotePeer))
                    );
                    break;
                case NetworkMessage.MessageOneofCase.GetTransactionsByHashesRequest:
                    OnGetTransactionsByHashesRequest?.Invoke(this,
                        (message.GetTransactionsByHashesRequest, SendTo(envelope.RemotePeer))
                    );
                    break;
                case NetworkMessage.MessageOneofCase.GetTransactionsByHashesReply:
                    OnGetTransactionsByHashesReply?.Invoke(this,
                        (message.GetTransactionsByHashesReply, envelope.PublicKey)
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
                            ClientWorker.SendOnce(
                                address,
                                _messageFactory.MessagesBatch(new[] {_messageFactory.HandshakeRequest(LocalNode)})
                            );
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