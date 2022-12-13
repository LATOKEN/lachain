using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Google.Protobuf;
using Lachain.Crypto;
using Lachain.Crypto.ECDSA;
using Lachain.Logger;
using Lachain.Networking.Hub;
using Lachain.Proto;
using Lachain.Utility;
using Lachain.Utility.Utils;
using PingReply = Lachain.Proto.PingReply;

namespace Lachain.Networking
{
    public abstract class NetworkManagerBase : INetworkManager, INetworkBroadcaster, IConsensusMessageDeliverer
    {
        public static ulong CycleDuration = 20; // in blocks
        private static readonly ICrypto Crypto = CryptoProvider.GetCrypto();

        private static readonly ILogger<NetworkManagerBase> Logger =
            LoggerFactory.GetLoggerForClass<NetworkManagerBase>();

        public Node LocalNode { get; }
        public IMessageFactory MessageFactory => _messageFactory;
        private readonly MessageFactory _messageFactory;
        private readonly HubConnector _hubConnector;
        private readonly ClientWorker _broadcaster;

        private readonly IDictionary<ECDSAPublicKey, ClientWorker> _clientWorkers =
            new ConcurrentDictionary<ECDSAPublicKey, ClientWorker>();
        
        private readonly ConcurrentDictionary<ulong, ECDSAPublicKey> _requestIdentifier =
            new ConcurrentDictionary<ulong, ECDSAPublicKey>();

        protected NetworkManagerBase(NetworkConfig networkConfig, EcdsaKeyPair keyPair, byte[] hubPrivateKey, 
            int version, int minPeerVersion)
        {
            if (networkConfig.Peers is null) throw new ArgumentNullException();
            _messageFactory = new MessageFactory(keyPair);
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
                string.Join(",", networkConfig.BootstrapAddresses),
                hubPrivateKey, networkConfig.NetworkName ?? "devnet", version, minPeerVersion, 
                networkConfig.NewChainId ?? throw new Exception("No newChainId in config"),
                networkConfig.HubMetricsPort ?? 7072, _messageFactory, networkConfig.HubLogLevel
            );
            _hubConnector.OnMessage += _HandleMessage;

            _broadcaster = new ClientWorker(new byte[33], _messageFactory, _hubConnector);
            if(networkConfig.CycleDuration is null)
                throw new Exception("Cycle Duration is not provided");
            CycleDuration = (ulong) networkConfig.CycleDuration;
        }

        public void AdvanceEra(ulong era)
        {
            var totalBatchesCount = _clientWorkers.Values.Sum(clientWorker => clientWorker.AdvanceEra(era));
            Logger.LogInformation($"Batches sent during era #{era - 1}: {totalBatchesCount}");
            OnAdvanceEra?.Invoke(this, era);
        }

        public void SendTo(ECDSAPublicKey publicKey, NetworkMessage message, NetworkMessagePriority priority)
        {
            var worker = GetClientWorker(publicKey);
            if (worker is null)
            {
                Logger.LogWarning($"No worker found for public key {publicKey.ToHex()}. Ignoring.");
                return;
            }
            
            var requestId = TrySaveRequest(publicKey, message);
            message.RequestId = requestId;
            worker.AddMsgToQueue(message, priority);
        }

        private ulong TrySaveRequest(ECDSAPublicKey publicKey, NetworkMessage message)
        {
            switch (message.MessageCase)
            {
                case NetworkMessage.MessageOneofCase.SyncBlocksRequest:
                    return SaveRequest(publicKey);
                default:
                    return 0;
            }
        }

        private ulong SaveRequest(ECDSAPublicKey publicKey)
        {
            ulong requestId;
            do
            {
                requestId = UInt64Utils.FromBytes(Crypto.GenerateRandomBytes(8));
            } while (!_requestIdentifier.TryAdd(requestId, publicKey));
            Logger.LogTrace($"Sent request with id {requestId} to peer {publicKey.ToHex()}");
            return requestId;
        }

        public void Start()
        {
            _broadcaster.Start();
            _hubConnector.Start();
        }

        // returns existing worker or create a new one if worker does not exists
        // not synchronized for the sake of performance
        private ClientWorker? GetClientWorker(ECDSAPublicKey publicKey)
        {
            if (_messageFactory.GetPublicKey().Equals(publicKey)) return null;
            if (_clientWorkers.TryGetValue(publicKey, out var existingWorker))
                return existingWorker;

            return CreateMsgChannel(publicKey);
        }

        // check if worker exists before creating and adding one
        // synchronized to avoid exception of adding existing key to dictionary
        [MethodImpl(MethodImplOptions.Synchronized)]
        private ClientWorker? CreateMsgChannel(ECDSAPublicKey publicKey)
        {
            if (_clientWorkers.TryGetValue(publicKey, out var existingWorker))
                return existingWorker;
            Logger.LogTrace($"Connecting to peer {publicKey.ToHex()}");
            var worker = new ClientWorker(publicKey, _messageFactory, _hubConnector);
            _clientWorkers.Add(publicKey, worker);
            worker.Start();
            OnWorkerCreated?.Invoke(this, worker);
            return worker;
        }

        public void IncPenalty(ECDSAPublicKey publicKey)
        {
            var worker = GetClientWorker(publicKey);
            if (worker is null)
            {
                Logger.LogWarning($"Got request to increase penalty for peer {publicKey.ToHex()} but worker is null");
                return;
            }
            worker.IncPenalty();
        }

        public void BanPeer(byte[] publicKey)
        {
            var worker = GetClientWorker(CryptoUtils.ToPublicKey(publicKey));
            if (worker is null)
            {
                Logger.LogWarning($"Got request to ban peer {publicKey.ToHex()} but worker is null");
                return;
            }
            worker.BanPeer();
        }

        public void RemoveFromBanList(byte[] publicKey)
        {
            var worker = GetClientWorker(CryptoUtils.ToPublicKey(publicKey));
            if (worker is null)
            {
                Logger.LogWarning($"Got request to unban peer {publicKey.ToHex()} but worker is null");
                return;
            }
            worker.RemoveFromBanList();
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
                batch.Sender.EncodeCompressed(), true))
            {
                Logger.LogError($"Incorrect signature received from sender {batch.Sender}, dropping");
                return;
            }

            var worker = GetClientWorker(batch.Sender);
            if (worker is null)
            {
                Logger.LogWarning($"Got batch from {batch.Sender.ToHex()} but cannot connect to him, skipping");
                return;
            }

            var envelope = new MessageEnvelope(batch.Sender, worker);
            var content = MessageBatchContent.Parser.ParseFrom(batch.Content);
            // if (content.Messages.Any(msg => msg.MessageCase == NetworkMessage.MessageOneofCase.ConsensusMessage))
            //     envelope.RemotePeer.AddMsgToQueue(_messageFactory.Ack(batch.MessageId));
            foreach (var message in content.Messages)
            {
                try
                {
                    HandleMessageUnsafe(message, envelope);
                }
                catch (Exception e)
                {
                    // add penalty for peer
                    envelope.RemotePeer.IncPenalty();
                    Logger.LogError($"Unexpected error occurred: {e}");
                }
            }
        }

        private Action<IMessage> SendTo(ClientWorker peer, ulong requestId)
        {
            return x =>
            {
                Logger.LogTrace($"Sending {x.GetType()} to {peer.PeerPublicKey.ToHex()}");
                NetworkMessage msg = x switch
                {
                    PingReply pingReply => new NetworkMessage { RequestId = requestId, PingReply = pingReply },
                    SyncBlocksReply syncBlockReply => new NetworkMessage
                        { RequestId = requestId, SyncBlocksReply = syncBlockReply },
                    SyncPoolReply syncPoolReply => new NetworkMessage
                        { RequestId = requestId, SyncPoolReply = syncPoolReply },
                    GetPeersReply getPeersReply => new NetworkMessage
                        { RequestId = requestId, GetPeersReply = getPeersReply },
                    _ => throw new InvalidOperationException()
                };
                // we should never reply with priority, requests can be spammed
                peer.AddMsgToQueue(msg, NetworkMessagePriority.ReplyMessage);
            };
        }

        private void HandleMessageUnsafe(NetworkMessage message, MessageEnvelope envelope)
        {
            // Logger.LogTrace($"Processing network message {message.MessageCase}");
            if (envelope.PublicKey is null) throw new InvalidOperationException();
            switch (message.MessageCase)
            {
                case NetworkMessage.MessageOneofCase.PingReply:
                    OnPingReply?.Invoke(this, (message.PingReply, envelope.PublicKey));
                    break;
                case NetworkMessage.MessageOneofCase.SyncBlocksRequest:
                    OnSyncBlocksRequest?.Invoke(this, (message.SyncBlocksRequest, SendTo(envelope.RemotePeer, message.RequestId)));
                    break;
                case NetworkMessage.MessageOneofCase.SyncBlocksReply:
                    if (IsRequested(message.RequestId, envelope.PublicKey))
                    {
                        OnSyncBlocksReply?.Invoke(this, (message.SyncBlocksReply, envelope.PublicKey));
                    }
                    else
                    {
                        Logger.LogWarning(
                            $"Got SyncBlocksReply from {envelope.PublicKey.ToHex()} with requestId {message.RequestId} "
                            + "but we never requestd such reply"
                        );
                        throw new Exception("Got unwanted SyncBlocksReply");
                    }
                    break;
                case NetworkMessage.MessageOneofCase.SyncPoolRequest:
                    throw new NotImplementedException("We do not support/need SyncPoolRequest yet");
                case NetworkMessage.MessageOneofCase.SyncPoolReply:
                    OnSyncPoolReply?.Invoke(this, (message.SyncPoolReply, envelope.PublicKey));
                    break;
                case NetworkMessage.MessageOneofCase.Ack:
                    throw new NotImplementedException("We do not support/need Ack yet");
                case NetworkMessage.MessageOneofCase.ConsensusMessage:
                    OnConsensusMessage?.Invoke(this, (message.ConsensusMessage, envelope.PublicKey));
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(message),
                        "Unable to resolve message type (" + message.MessageCase + ") from protobuf structure");
            }
        }

        private bool IsRequested(ulong requestId, ECDSAPublicKey peer)
        {
            if (_requestIdentifier.TryGetValue(requestId, out var publicKey))
            {
                if (peer.Equals(publicKey))
                {
                    return _requestIdentifier.TryRemove(requestId, out var _);
                }
            }
            return false;
        }

        public void BroadcastLocalTransaction(TransactionReceipt e)
        {
            Broadcast(MessageFactory.SyncPoolReply(new[] {e}), NetworkMessagePriority.PoolSyncMessage);
        }

        public void Broadcast(NetworkMessage networkMessage, NetworkMessagePriority priority)
        {
            _broadcaster.AddMsgToQueue(networkMessage, priority);
        }

        private void Stop()
        {
            foreach (var client in _clientWorkers.Values)
            {
                client.Stop();
            }

            _broadcaster!.Stop();
        }

        public void Dispose()
        {
            Stop();
            _hubConnector.Dispose();
            _clientWorkers.Clear();
        }

        public static ulong CycleNumber(ulong era)
        {
            return era / CycleDuration;
        }

        public static ulong BlockInCycle(ulong era)
        {
            return era % CycleDuration;
        }

        public event EventHandler<(PingReply message, ECDSAPublicKey publicKey)>? OnPingReply;

        public event EventHandler<(SyncBlocksRequest message, Action<SyncBlocksReply> callback)>?
            OnSyncBlocksRequest;

        public event EventHandler<(SyncBlocksReply message, ECDSAPublicKey address)>? OnSyncBlocksReply;
        public event EventHandler<(SyncPoolReply message, ECDSAPublicKey address)>? OnSyncPoolReply;
        public event EventHandler<(ConsensusMessage message, ECDSAPublicKey publicKey)>? OnConsensusMessage;
        public event EventHandler<ulong>? OnAdvanceEra;
        public event EventHandler<ClientWorker>? OnWorkerCreated;
    }
}