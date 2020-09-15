using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Google.Protobuf;
using Lachain.Crypto;
using Lachain.Crypto.ECDSA;
using Lachain.Logger;
using Lachain.Networking.Hub;
using Lachain.Proto;
using Lachain.Storage.Repositories;
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
        private readonly HubConnector _hubConnector;

        private readonly IDictionary<ECDSAPublicKey, ClientWorker> _clientWorkers =
            new ConcurrentDictionary<ECDSAPublicKey, ClientWorker>();

        protected NetworkManagerBase(NetworkConfig networkConfig, EcdsaKeyPair keyPair, IPeerRepository peerRepository)
        {
            if (networkConfig.Peers is null) throw new ArgumentNullException();
            _messageFactory = new MessageFactory(keyPair);
            _peerManager = new PeerManager(peerRepository, this, _messageFactory, networkConfig);
            LocalNode = new Node
            {
                Version = 0,
                Timestamp = (ulong) DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                PublicKey = keyPair.PublicKey,
                Nonce = (uint) new Random().Next(1 << 30),
                BlockHeight = 0,
                Agent = "Lachain-v0.0-dev"
            };
            _hubConnector = new HubConnector(
                $"127.0.0.1:{networkConfig.Port}", networkConfig.BootstrapAddress, _messageFactory
            );
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
            var peers = _peerManager.Start();
            foreach (var peer in peers.Select(peer => peer.PublicKey))
                Connect(peer);
            _peerManager.BroadcastGetPeersRequest();
        }

        private ClientWorker? Connect(ECDSAPublicKey publicKey)
        {
            if (_messageFactory.GetPublicKey().Equals(publicKey)) return null;
            if (_clientWorkers.TryGetValue(publicKey, out var existingWorker))
                return existingWorker;

            Logger.LogTrace($"Connecting to peer {publicKey.ToHex()}");
            var worker = new ClientWorker(publicKey, _messageFactory, _hubConnector);
            _clientWorkers.Add(publicKey, worker);
            worker.Start();
            return worker;
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

            var worker = Connect(batch.Sender);
            if (worker is null)
            {
                Logger.LogWarning($"Got batch from {batch.Sender.ToHex()} but cannot connect to him, skipping");
                return;
            }

            var envelope = new MessageEnvelope(batch.Sender, worker);
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

        private Action<IMessage> SendTo(ClientWorker peer)
        {
            return x =>
            {
                Logger.LogTrace($"Sending {x.GetType()} to {peer.PeerPublicKey.ToHex()}");
                NetworkMessage msg = x switch
                {
                    PingReply pingReply => new NetworkMessage {PingReply = pingReply},
                    SyncBlocksReply syncBlockReply => new NetworkMessage {SyncBlocksReply = syncBlockReply},
                    SyncPoolReply syncPoolReply => new NetworkMessage {SyncPoolReply = syncPoolReply},
                    GetPeersReply getPeersReply => new NetworkMessage {GetPeersReply = getPeersReply},
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
                case NetworkMessage.MessageOneofCase.SyncBlocksRequest:
                    OnSyncBlocksRequest?.Invoke(this, (message.SyncBlocksRequest, SendTo(envelope.RemotePeer)));
                    break;
                case NetworkMessage.MessageOneofCase.SyncBlocksReply:
                    OnSyncBlocksReply?.Invoke(this, (message.SyncBlocksReply, envelope.PublicKey));
                    _peerManager.UpdatePeerTimestamp(envelope.PublicKey);
                    break;
                case NetworkMessage.MessageOneofCase.SyncPoolRequest:
                    OnSyncPoolRequest?.Invoke(this, (message.SyncPoolRequest, SendTo(envelope.RemotePeer)));
                    break;
                case NetworkMessage.MessageOneofCase.SyncPoolReply:
                    OnSyncPoolReply?.Invoke(this, (message.SyncPoolReply, envelope.PublicKey));
                    _peerManager.UpdatePeerTimestamp(envelope.PublicKey);
                    break;
                case NetworkMessage.MessageOneofCase.GetPeersRequest:
                    var peersToBroadcast = _peerManager.GetPeersToBroadcast();
                    SendTo(envelope.RemotePeer)(new GetPeersReply {Peers = {peersToBroadcast}});
                    break;
                case NetworkMessage.MessageOneofCase.GetPeersReply:
                    var peers = _peerManager.HandlePeersFromPeer(message.GetPeersReply.Peers);
                    foreach (var peer in peers) Connect(peer.PublicKey);
                    break;
                case NetworkMessage.MessageOneofCase.Ack:
                    envelope.RemotePeer.ReceiveAck(message.Ack.MessageId);
                    break;
                case NetworkMessage.MessageOneofCase.ConsensusMessage:
                    OnConsensusMessage?.Invoke(this, (message.ConsensusMessage, envelope.PublicKey));
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(message),
                        "Unable to resolve message type (" + message.MessageCase + ") from protobuf structure");
            }
        }

        public void BroadcastLocalTransaction(TransactionReceipt e)
        {
            Broadcast(MessageFactory.SyncPoolReply(new[] {e}));
        }

        public void Broadcast(NetworkMessage networkMessage)
        {
            foreach (var client in _clientWorkers.Values)
            {
                client.Send(networkMessage);
            }
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

        public event EventHandler<(SyncBlocksRequest message, Action<SyncBlocksReply> callback)>?
            OnSyncBlocksRequest;

        public event EventHandler<(SyncBlocksReply message, ECDSAPublicKey address)>? OnSyncBlocksReply;
        public event EventHandler<(SyncPoolRequest message, Action<SyncPoolReply> callback)>? OnSyncPoolRequest;
        public event EventHandler<(SyncPoolReply message, ECDSAPublicKey address)>? OnSyncPoolReply;
        public event EventHandler<(ConsensusMessage message, ECDSAPublicKey publicKey)>? OnConsensusMessage;
    }
}