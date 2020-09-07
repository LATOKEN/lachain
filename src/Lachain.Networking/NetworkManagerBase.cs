using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using Google.Protobuf;
using Lachain.Crypto;
using Lachain.Crypto.ECDSA;
using Lachain.Logger;
using Lachain.Networking.Hub;
using Lachain.Proto;
using Lachain.Utility.Utils;
using PingReply = Lachain.Proto.PingReply;

namespace Lachain.Networking
{
    public abstract class NetworkManagerBase : INetworkManager, INetworkBroadcaster, IConsensusMessageDeliverer
    {
        private static readonly ICrypto Crypto = CryptoProvider.GetCrypto();

        private static readonly ILogger<NetworkManagerBase> Logger =
            LoggerFactory.GetLoggerForClass<NetworkManagerBase>();

        public Node LocalNode { get; }
        public IMessageFactory MessageFactory => _messageFactory;
        private readonly IPeerManager _peerManager;
        private readonly MessageFactory _messageFactory;
        private readonly EcdsaKeyPair _keyPair;
        private readonly HubConnector _hubConnector;
        private readonly NetworkConfig _networkConfig;

        private readonly IDictionary<ECDSAPublicKey, ClientWorker> _clientWorkers =
            new ConcurrentDictionary<ECDSAPublicKey, ClientWorker>();

        protected NetworkManagerBase(NetworkConfig networkConfig, EcdsaKeyPair keyPair, IPeerManager peerManager)
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
            _hubConnector = new HubConnector(networkConfig.HubAddress, _messageFactory);
            _hubConnector.OnMessage += _HandleMessage;
        }

        public void AdvanceEra(long era)
        {
            foreach (var clientWorker in _clientWorkers.Values)
            {
                clientWorker.AdvanceEra(era);
            }
        }

        public void SendTo(ECDSAPublicKey publicKey, NetworkMessage message)
        {
            Connect(publicKey)?.Send(message);
        }

        public void Start()
        {
            _hubConnector.Start();
            var peers = _peerManager.Start(_networkConfig, this, _keyPair);
            foreach (var peer in peers.Select(peer => peer.PublicKey!))
                Connect(peer);
            _peerManager.BroadcastGetPeersRequest();
        }

        private readonly object _hasPeersToConnect = new object();

        private ClientWorker? Connect(ECDSAPublicKey publicKey)
        {
            if (_messageFactory.GetPublicKey().Equals(publicKey)) return null;
            lock (_hasPeersToConnect)
            {
                if (_clientWorkers.TryGetValue(publicKey, out var existingWorker))
                    return existingWorker;

                Logger.LogTrace($"Connecting to peer {publicKey.ToHex()}");
                var worker = new ClientWorker(publicKey, _messageFactory, _hubConnector);
                _clientWorkers.Add(publicKey, worker);
                worker.Start();
                var peerJoinMsg = _messageFactory.PeerJoinRequest(_peerManager.GetCurrentPeer());
                worker.Send(peerJoinMsg);
                Monitor.PulseAll(_hasPeersToConnect);
                return worker;
            }
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

            if (batch?.Content is null || batch.Signature is null)
            {
                Logger.LogError("Unable to parse protocol message");
                Logger.LogTrace($"Original message bytes: {buffer.ToHex()}");
                return;
            }

            if (!Crypto.VerifySignature(batch.Content.ToByteArray(), batch.Signature.Encode(),
                batch.Sender.EncodeCompressed()))
            {
                Logger.LogError($"Incorrect signature received from sender {batch.Sender}, dropping");
                return;
            }

            if (!_clientWorkers.TryGetValue(batch.Sender, out var worker))
            {
                if (batch.Sender.Equals(_messageFactory.GetPublicKey())) return;
                try
                {
                    var content = MessageBatchContent.Parser.ParseFrom(batch.Content);
                    var joinMsg = content.Messages.FirstOrDefault(msg =>
                        msg.MessageCase == NetworkMessage.MessageOneofCase.PeerJoinRequest);

                    if (joinMsg != null)
                    {
                        joinMsg.PeerJoinRequest.Peer.Host = CheckLocalConnection(joinMsg.PeerJoinRequest.Peer.Host);
                        _peerManager.AddPeer(joinMsg.PeerJoinRequest.Peer, batch.Sender);
                        var peer = Connect(batch.Sender)!;
                        peer.Send(_messageFactory.Ack(batch.MessageId));
                        return;
                    }
                }
                catch (Exception e)
                {
                    Logger.LogError($"Exception was thrown while peer join: {e}");
                }

                Logger.LogTrace(
                    $"Message from unrecognized peer {batch.Sender.ToHex()} or invalid signature, skipping");
                return;
            }

            var envelope = new MessageEnvelope(batch.Sender, worker);

            {
                var content = MessageBatchContent.Parser.ParseFrom(batch.Content);
                if (content.Messages.Any(msg => msg.MessageCase == NetworkMessage.MessageOneofCase.ConsensusMessage))
                    envelope.RemotePeer.Send(_messageFactory.Ack(batch.MessageId));
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

        private Action<IMessage> SendTo(ClientWorker peer)
        {
            return x =>
            {
                Logger.LogTrace($"Sending {x.GetType()} to {peer.PeerPublicKey.ToHex()}");
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
                peer.Send(msg);
            };
        }

        private void HandleMessageUnsafe(NetworkMessage message, MessageEnvelope envelope)
        {
            Logger.LogTrace($"Processing network message {message.MessageCase}");
            if (envelope.PublicKey is null) throw new InvalidOperationException();
            switch (message.MessageCase)
            {
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
                    OnGetPeersRequest?.Invoke(this, (message.GetPeersRequest, SendTo(envelope.RemotePeer)));
                    break;
                case NetworkMessage.MessageOneofCase.GetPeersReply:
                    OnGetPeersReply?.Invoke(this, (message.GetPeersReply, envelope.PublicKey, Connect));
                    break;
                case NetworkMessage.MessageOneofCase.Ack:
                    envelope.RemotePeer.ReceiveAck(message.Ack.MessageId);
                    break;
                case NetworkMessage.MessageOneofCase.PeerJoinRequest:
                    Logger.LogTrace($"Peer join message received from known peer {message.PeerJoinRequest.Peer.Host}");
                    message.PeerJoinRequest.Peer.Host = CheckLocalConnection(message.PeerJoinRequest.Peer.Host);
                    var peerAddress = PeerManager.ToPeerAddress(message.PeerJoinRequest.Peer, envelope.PublicKey);
                    if (!_clientWorkers.ContainsKey(envelope.PublicKey))
                    {
                        _peerManager.AddPeer(message.PeerJoinRequest.Peer, envelope.PublicKey);
                        Connect(envelope.PublicKey);
                        Logger.LogDebug($"Peer reconnected with new address: {peerAddress}");
                    }
                    else
                    {
                        Logger.LogDebug($"Connection already established: {peerAddress}");
                    }

                    break;
                case NetworkMessage.MessageOneofCase.ConsensusMessage:
                    OnConsensusMessage?.Invoke(this, (message.ConsensusMessage, envelope.PublicKey));
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(message),
                        "Unable to resolve message type (" + message.MessageCase + ") from protobuf structure");
            }
        }


        public string CheckLocalConnection(string host)
        {
            return _peerManager.CheckLocalConnection(host);
        }

        public void BroadcastLocalTransaction(TransactionReceipt e)
        {
            var message = MessageFactory.GetTransactionsByHashesReply(new[] {e}) ??
                          throw new InvalidOperationException();
            Broadcast(message);
        }

        public void Broadcast(NetworkMessage networkMessage)
        {
            foreach (var client in _clientWorkers.Values)
            {
                client.Send(networkMessage);
            }
        }

        public bool IsSelfConnect(IPAddress ipAddress)
        {
            return NetworkingUtils.IsSelfConnect(ipAddress, _peerManager.GetExternalIp());
        }

        private void Stop()
        {
            _peerManager.Stop();
            foreach (var client in _clientWorkers.Values)
            {
                client.Stop();
            }
        }

        public void Dispose()
        {
            Stop();
            _hubConnector.Dispose();
            foreach (var client in _clientWorkers.Values)
            {
                client.Dispose();
            }

            _clientWorkers.Clear();
        }

        public event EventHandler<(PingRequest message, Action<PingReply> callback)>? OnPingRequest;
        public event EventHandler<(PingReply message, ECDSAPublicKey publicKey)>? OnPingReply;

        public event EventHandler<(GetBlocksByHashesRequest message, Action<GetBlocksByHashesReply> callback)>?
            OnGetBlocksByHashesRequest;

        public event EventHandler<(GetBlocksByHashesReply message, ECDSAPublicKey address)>? OnGetBlocksByHashesReply;

        public event
            EventHandler<(GetBlocksByHeightRangeRequest message, Action<GetBlocksByHeightRangeReply> callback)>?
            OnGetBlocksByHeightRangeRequest;

        public event
            EventHandler<(GetPeersRequest message, Action<GetPeersReply> callback)>? OnGetPeersRequest;

        public event
            EventHandler<(GetBlocksByHeightRangeReply message, Action<GetBlocksByHashesRequest> callback)>?
            OnGetBlocksByHeightRangeReply;

        public event
            EventHandler<(GetTransactionsByHashesRequest message, Action<GetTransactionsByHashesReply> callback)>?
            OnGetTransactionsByHashesRequest;

        public event EventHandler<(GetTransactionsByHashesReply message, ECDSAPublicKey address)>?
            OnGetTransactionsByHashesReply;

        public event
            EventHandler<(GetPeersReply message, ECDSAPublicKey address, Func<ECDSAPublicKey, ClientWorker?> connect)>?
            OnGetPeersReply;

        public event EventHandler<(ConsensusMessage message, ECDSAPublicKey publicKey)>? OnConsensusMessage;
    }
}