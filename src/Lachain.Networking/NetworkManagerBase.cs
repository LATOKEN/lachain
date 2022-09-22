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
using Lachain.Storage.Repositories;
using Lachain.Utility;
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
        private readonly MessageFactory _messageFactory;
        private readonly HubConnector _hubConnector;
        private readonly ClientWorker _broadcaster;
        private bool _validatorChannelConnected = false;
        private bool _started = false;

        private readonly IDictionary<ECDSAPublicKey, ClientWorker> _clientWorkers =
            new ConcurrentDictionary<ECDSAPublicKey, ClientWorker>();

        private List<ECDSAPublicKey> _connectedValidators = new List<ECDSAPublicKey>();

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
        }

        public void AdvanceEra(ulong era)
        {
            var totalBatchesCount = _clientWorkers.Values.Sum(clientWorker => clientWorker.AdvanceEra(era));
            Logger.LogInformation($"Batches sent during era #{era - 1}: {totalBatchesCount}");
        }

        public void SendTo(ECDSAPublicKey publicKey, NetworkMessage message, NetworkMessagePriority priority)
        {
            GetClientWorker(publicKey)?.AddMsgToQueue(message, priority);
        }

        public void Start()
        {
            _broadcaster.Start();
            _hubConnector.Start();
            _started = true;
        }

        public void ConnectValidatorChannel(List<ECDSAPublicKey> validators)
        {
            if (!_started) return;
            
            if (!_validatorChannelConnected)
            {
                _validatorChannelConnected = true;
                _hubConnector.ConnectValidatorChannel();
            }

            validators = validators.OrderBy(
                x => x, new ComparisonUtils.ECDSAPublicKeyComparer()).ToList();
            _connectedValidators = _connectedValidators.OrderBy(
                x => x, new ComparisonUtils.ECDSAPublicKeyComparer()).ToList();

            var validatorsToDisconnect = RemovePublicKeys(_connectedValidators, validators);
            foreach (var publicKey in validatorsToDisconnect)
            {
                GetClientWorker(publicKey)?.SetValidator(false);
            }

            var validatorsToConnect = RemovePublicKeys(validators, _connectedValidators);
            foreach (var publicKey in validatorsToConnect)
            {
                GetClientWorker(publicKey)?.SetValidator(true);
            }
            
            lock (_connectedValidators)
            {
                _connectedValidators.Clear();
                _connectedValidators = new List<ECDSAPublicKey>(validators);
            }
        }
        
        public void DisconnectValidatorChannel()
        {
            if (!_started) return;

            foreach (var publicKey in _connectedValidators)
            {
                GetClientWorker(publicKey)?.SetValidator(false);
            }
            lock (_connectedValidators)
                _connectedValidators.Clear();

            if (_validatorChannelConnected)
            {
                _validatorChannelConnected = false;
                _hubConnector.DisconnectValidatorChannel();
            }
        }

        // both input lists need to be sorted and no duplicate element allowed
        // removing items from source which is present in keysToRemove
        // in O(source.Count + keysToRemove.Count) complexity
        private List<ECDSAPublicKey> RemovePublicKeys(
            List<ECDSAPublicKey> source,
            List<ECDSAPublicKey> keysToRemove
        )
        {
            var res = new List<ECDSAPublicKey>();
            int iter = 0;
            foreach (var publicKey in source)
            {
                bool found = false;
                while (iter < keysToRemove.Count)
                {
                    var key = keysToRemove[iter];
                    var compare = key.Buffer.Cast<IComparable<byte>>().
                        CompareLexicographically(publicKey.Buffer);
                    if (compare == 0) found = true;
                    else if (compare > 0) break;

                    iter++;
                }

                if (!found) res.Add(publicKey);
            }
            return res;
        }

        private ClientWorker? GetClientWorker(ECDSAPublicKey publicKey)
        {
            if (_messageFactory.GetPublicKey().Equals(publicKey)) return null;
            if (_clientWorkers.TryGetValue(publicKey, out var existingWorker))
                return existingWorker;

            return CreateMsgChannel(publicKey);
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        private ClientWorker? CreateMsgChannel(ECDSAPublicKey publicKey)
        {
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
                    OnSyncBlocksRequest?.Invoke(this, (message.SyncBlocksRequest, SendTo(envelope.RemotePeer)));
                    break;
                case NetworkMessage.MessageOneofCase.SyncBlocksReply:
                    OnSyncBlocksReply?.Invoke(this, (message.SyncBlocksReply, envelope.PublicKey));
                    break;
                case NetworkMessage.MessageOneofCase.SyncPoolRequest:
                    OnSyncPoolRequest?.Invoke(this, (message.SyncPoolRequest, SendTo(envelope.RemotePeer)));
                    break;
                case NetworkMessage.MessageOneofCase.SyncPoolReply:
                    OnSyncPoolReply?.Invoke(this, (message.SyncPoolReply, envelope.PublicKey));
                    break;
                case NetworkMessage.MessageOneofCase.Ack:
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

        public event EventHandler<(PingReply message, ECDSAPublicKey publicKey)>? OnPingReply;

        public event EventHandler<(SyncBlocksRequest message, Action<SyncBlocksReply> callback)>?
            OnSyncBlocksRequest;

        public event EventHandler<(SyncBlocksReply message, ECDSAPublicKey address)>? OnSyncBlocksReply;
        public event EventHandler<(SyncPoolRequest message, Action<SyncPoolReply> callback)>? OnSyncPoolRequest;
        public event EventHandler<(SyncPoolReply message, ECDSAPublicKey address)>? OnSyncPoolReply;
        public event EventHandler<(ConsensusMessage message, ECDSAPublicKey publicKey)>? OnConsensusMessage;
    }
}