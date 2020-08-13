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
using Lachain.Networking.Hub;
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

        private static int ConnectionLimit { get; } = 10;

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
        private readonly IPeerManager _peerManager;

        private readonly IDictionary<PeerAddress, ClientWorker> _clientWorkers =
            new ConcurrentDictionary<PeerAddress, ClientWorker>();

        private readonly MessageFactory _messageFactory;
        
        private readonly EcdsaKeyPair _keyPair;

        private readonly ServerWorker _serverWorker;

        private readonly NetworkConfig _networkConfig;

        private IList<PeerAddress> _currentConsensusPeers;
        public ConsensusNetworkManager ConsensusNetworkManager { get; }

        public event EventHandler<(PingRequest message, Action<PingReply> callback)>? OnPingRequest;
        public event EventHandler<(PingReply message, ECDSAPublicKey publicKey)>? OnPingReply;

        public event EventHandler<(GetBlocksByHashesRequest message, Action<GetBlocksByHashesReply> callback)>?
            OnGetBlocksByHashesRequest;

        public event EventHandler<(GetBlocksByHashesReply message, ECDSAPublicKey address)>? OnGetBlocksByHashesReply;

        public event EventHandler<(GetBlocksByHeightRangeRequest message, Action<GetBlocksByHeightRangeReply> callback)>
            ?
            OnGetBlocksByHeightRangeRequest;
        
        public event EventHandler<(GetPeersRequest message, Action<GetPeersReply> callback)>
            ?
            OnGetPeersRequest;

        public event EventHandler<(GetBlocksByHeightRangeReply message, Action<GetBlocksByHashesRequest> callback)>?
            OnGetBlocksByHeightRangeReply;

        public event EventHandler<(GetTransactionsByHashesRequest message, Action<GetTransactionsByHashesReply> callback
                )>?
            OnGetTransactionsByHashesRequest;

        public event EventHandler<(GetTransactionsByHashesReply message, ECDSAPublicKey address)>?
            OnGetTransactionsByHashesReply;

        public event EventHandler<(GetPeersReply message, ECDSAPublicKey address, Func<PeerAddress, IRemotePeer> connect)>?
            OnGetPeersReply;

        public event EventHandler<(ConsensusMessage message, ECDSAPublicKey publicKey)>? OnConsensusMessage;

        public IEnumerable<PeerAddress> GetConnectedPeers()
        {
            return _clientWorkers.Keys;
        }

        public NetworkManagerBase(NetworkConfig networkConfig, EcdsaKeyPair keyPair, IPeerManager peerManager)
        {
            if (networkConfig?.Peers is null) throw new ArgumentNullException();
            _networkConfig = networkConfig;
            _peerManager = peerManager;
            _messageFactory = new MessageFactory(keyPair);
            _keyPair = keyPair;
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
            ConsensusNetworkManager = new ConsensusNetworkManager(_messageFactory, networkConfig, LocalNode, peerManager, CheckLocalConnection);
            _serverWorker = new ServerWorker(networkConfig.Address, networkConfig.Port);
            _serverWorker.OnMessage += _HandleMessage;
            _serverWorker.OnError += (sender, error) => Logger.LogError($"Server error: {error}");
            ConsensusNetworkManager.OnMessage += (sender, e) => OnConsensusMessage?.Invoke(sender, e);
            _currentConsensusPeers = new List<PeerAddress>();
        }

        public void ConnectToValidators(IEnumerable<ECDSAPublicKey> validators)
        {
            var ecdsaPublicKeys = validators.ToList();
            _currentConsensusPeers = _peerManager.GetPeerAddressesByPublicKeys(ecdsaPublicKeys);
            // TODO: seems it's not so fatal
            // if (_currentConsensusPeers.Count != ecdsaPublicKeys.Count) throw new Exception("Peers count mismatch");
            ConsensusNetworkManager.ConnectToValidators(_currentConsensusPeers.Select(x => x.PublicKey!));
        }

        public void SendToPeerByPublicKey(ECDSAPublicKey publicKey, NetworkMessage message)
        {
            GetPeerByPublicKey(publicKey)?.Send(message);
        }

        public bool IsReady => _serverWorker != null && _serverWorker.IsActive;

        public void Start()
        {
            _serverWorker.Start();
            Task.Factory.StartNew(ConnectWorker, TaskCreationOptions.LongRunning);
            var peers = _peerManager.Start(_networkConfig, this, _keyPair);
            foreach (var peer in peers)
                Connect(peer);
            _peerManager.BroadcastGetPeersRequest();
            Task.Factory.StartNew(HubWorker, TaskCreationOptions.LongRunning);
        }

        public IRemotePeer? GetPeerByPublicKey(ECDSAPublicKey publicKey)
        {
            return _clientWorkers.Values
                .FirstOrDefault(x => publicKey.Equals(x?.PeerPublicKey));
        }

        public bool IsConnected(PeerAddress address)
        {
            return _clientWorkers.ContainsKey(address);
        }

        public bool IsSelfConnect(IPAddress ipAddress)
        {
            var localHost = new IPAddress(0x0100007f);
            if (ipAddress.Equals(localHost))
                return true;

            if (ipAddress.Equals(IPAddress.Parse(_peerManager.GetExternalIp())))
                return true;
            
            try
            {
                var host = Dns.GetHostEntry(Dns.GetHostName());
                if (host.AddressList.Contains(ipAddress))
                    return true;
            }
            catch (Exception e)
            {
                Logger.LogWarning("Unable to GetHostName()");
            }

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
            lock (_hasPeersToConnect)
            {
                if (_clientWorkers.TryGetValue(address, out var worker))
                {
                    Logger.LogTrace($"Found worker for peer {address}");
                    return worker;
                }

                Logger.LogTrace($"Not found worker for peer {address}");
                if (IsSelfConnect(IPAddress.Parse(address.Host)) && _networkConfig.Port == address.Port)
                {
                    Logger.LogTrace($"Self connect, skipping");
                    return null;
                }

                if (address.PublicKey is null)
                    throw new InvalidOperationException($"Cannot connect to peer {address}: no public key");
                
                var stakers = _peerManager.GetStakers();
                var nonConsensusPeerCount = _clientWorkers.Count(worker => !stakers.Contains(worker.Key.PublicKey!));
                var isConsensusPeer = stakers.Contains(address.PublicKey) || stakers.Length == 0;

                Logger.LogTrace($"Non consensus peers count before connection: {nonConsensusPeerCount}");
                if (!isConsensusPeer && nonConsensusPeerCount >= ConnectionLimit)
                {
                    Logger.LogTrace($"Connections limit reached. Peer is not connected: {address}");
                    return null;
                }
                Logger.LogTrace($"Connecting to peer {address}");
                
                worker = new ClientWorker(address, address.PublicKey!, _messageFactory);
                _clientWorkers.Add(address, worker);
                Logger.LogTrace($"Added worker for peer {address}");
                worker.Start();
                var peerJoinMsg = _messageFactory.PeerJoinRequest(_peerManager.GetCurrentPeer());
                SendToPeerByPublicKey(address.PublicKey!, peerJoinMsg);
                Monitor.PulseAll(_hasPeersToConnect);
                // Logger.LogTrace($"Successfully connected to peer {address}");
                return worker;
            }
        }

        public void Disconnect(ECDSAPublicKey publicKey)
        {
            lock (_hasPeersToConnect)
            {
                var workerAddresses = _clientWorkers.Keys.ToArray();
                foreach (var workerAddress in workerAddresses)
                {
                    if (!workerAddress.PublicKey!.Equals(publicKey)) continue;
                    if (_clientWorkers.TryGetValue(workerAddress, out var worker))
                    {
                        worker.Stop();
                    }
                    Logger.LogInformation($"Disconnecting from peer {workerAddress}");
                    _clientWorkers.Remove(workerAddress);
                }
                Monitor.PulseAll(_hasPeersToConnect);
            }
        }

        private void _HandshakeRequest(HandshakeRequest request)
        {
            if (request.Node.PublicKey is null)
                throw new Exception("Public key can't be null");
            var address = PeerAddress.FromNode(request.Node, CheckLocalConnection);
            var peer = _clientWorkers.TryGetValue(address, out var clientWorker)
                ? clientWorker
                : Connect(address);
            var port = ConsensusNetworkManager.GetReadyForConnect(request.Node.PublicKey);
            Logger.LogTrace($"Got handshake request from {address}");
            peer?.Send(_messageFactory.HandshakeReply(LocalNode, port));
        }

        private void _HandshakeReply(HandshakeReply reply)
        {
            var address = PeerAddress.FromNode(reply.Node, CheckLocalConnection);
            address.Port = (int) reply.Port;
            Logger.LogTrace($"Got handshake reply from {reply.Node.Address}");
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
                try
                {
                    var content = MessageBatchContent.Parser.ParseFrom(batch.Content);
                    var joinMsg = content.Messages.FirstOrDefault(msg =>
                        msg.MessageCase == NetworkMessage.MessageOneofCase.PeerJoinRequest);

                    if (joinMsg != null)
                    {
                        joinMsg.PeerJoinRequest.Peer.Host = CheckLocalConnection(joinMsg.PeerJoinRequest.Peer.Host);
                        _peerManager.AddPeer(joinMsg.PeerJoinRequest.Peer, publicKey);
                        var peer = Connect(PeerManager.ToPeerAddress(joinMsg.PeerJoinRequest.Peer, publicKey));
                        if (peer == null)
                        {
                            Logger.LogDebug(
                                $"Peer added to storage, but not connected: {publicKey.ToHex()}");
                            return;
                        }
                        peer!.Send(_messageFactory.Ack(batch.MessageId));
                        return;
                    }
                }
                catch (Exception e)
                {
                    Logger.LogError($"Exception was thrown while peer join msg parsing: {e}");
                }
                Logger.LogTrace(
                    $"Message from unrecognized peer {publicKey.ToHex()} or invalid signature, skipping");
                return;
            }

            GetPeerByPublicKey(envelope.PublicKey)?.Send(_messageFactory.Ack(batch.MessageId));
            {
                var content = MessageBatchContent.Parser.ParseFrom(batch.Content);
                foreach (var message in content.Messages)
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
        }

        public string CheckLocalConnection(string host)
        {
            return _peerManager.CheckLocalConnection(host);
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
                    GetPeersReply getPeersReply => new NetworkMessage
                    {
                        GetPeersReply = getPeersReply
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
                case NetworkMessage.MessageOneofCase.GetPeersRequest:
                    Logger.LogTrace($"Got GetPeersRequest");
                    Logger.LogTrace(envelope.RemotePeer!.Address.ToString());
                    OnGetPeersRequest?.Invoke(this,
                        (message.GetPeersRequest, SendTo(envelope.RemotePeer))
                    );
                    break;
                case NetworkMessage.MessageOneofCase.GetPeersReply:
                    OnGetPeersReply?.Invoke(this,
                        (message.GetPeersReply, envelope.PublicKey, Connect)
                    );
                    break;
                case NetworkMessage.MessageOneofCase.Ack:
                    GetPeerByPublicKey(envelope.PublicKey)?.ReceiveAck(message.Ack.MessageId);
                    break;
                case NetworkMessage.MessageOneofCase.PeerJoinRequest:
                    Logger.LogTrace($"Peer join message received from known peer");
                    message.PeerJoinRequest.Peer.Host = CheckLocalConnection(message.PeerJoinRequest.Peer.Host);
                    var peerAddress = PeerManager.ToPeerAddress(message.PeerJoinRequest.Peer, envelope.PublicKey);
                    if (!_clientWorkers.ContainsKey(peerAddress))
                    {
                        _peerManager.AddPeer(message.PeerJoinRequest.Peer, envelope.PublicKey);
                        Disconnect(envelope.PublicKey);
                        Connect(peerAddress);
                        Logger.LogDebug($"Peer reconnected with new address: {peerAddress}");
                    }
                    else
                    {
                        Logger.LogDebug($"Connection already established: {peerAddress}");
                    }

                    break;
                case NetworkMessage.MessageOneofCase.ConsensusMessage:
                    ConsensusNetworkManager.HandleConsensusMessage(
                        this,
                        (message.ConsensusMessage, envelope.PublicKey)
                    );
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(message),
                        "Unable to resolve message type (" + message.MessageCase + ") from protobuf structure");
            }
        }

        private void HubWorker()
        {
            while (Thread.CurrentThread.IsAlive)
            {
                var ts = TimeUtils.CurrentTimeMillis();
                var messages = CommunicationHub.Receive(
                    _messageFactory.GetPublicKey(),
                    ts,
                    _messageFactory.SignCommunicationHubReceive(ts)
                );
                foreach (var message in messages)
                {
                    _HandleMessage(this, message);
                }

                Thread.Sleep(TimeSpan.FromMilliseconds(1_000));
            }
        }

        private void ConnectWorker()
        {
            var thread = Thread.CurrentThread;
            while (thread.IsAlive)
            {
                lock (_hasPeersToConnect)
                {
                    Monitor.Wait(_hasPeersToConnect, TimeSpan.FromSeconds(5));
                    if (!_clientWorkers.Any())
                        continue;
                    foreach (var address in _currentConsensusPeers)
                    {
                        try
                        {
                            Logger.LogTrace($"Sending handshake request to {address}");
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
                    var stakers = _peerManager.GetStakers();
                    if (!stakers.Any())
                        continue;
                    var nonConsensusPeers = _clientWorkers.Where(worker => !stakers.Contains(worker.Key.PublicKey!)).ToArray();
                    if (nonConsensusPeers.Length < ConnectionLimit) continue;

                    PeerAddress? oldestPeer = null;
                    var oldestPeerTime = uint.MaxValue;
                    foreach (var peerAddress in nonConsensusPeers)
                    {
                        var peer = _peerManager.GetPeerByPublicKey(peerAddress.Key.PublicKey!);
                        if (peer!.Timestamp >= oldestPeerTime) continue;
                        oldestPeer = peerAddress.Key;
                        oldestPeerTime = peer.Timestamp;
                    }
                    Monitor.Exit(_hasPeersToConnect);
                    Disconnect(oldestPeer!.PublicKey!);
                }
            }
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
            _peerManager.Stop();
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