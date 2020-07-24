using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using Google.Protobuf;
using Lachain.Crypto;
using Lachain.Crypto.ECDSA;
using Lachain.Logger;
using Lachain.Networking.ZeroMQ;
using Lachain.Proto;
using Lachain.Storage.Repositories;
using Lachain.Utility.Utils;
using NLog.Fluent;
using PingReply = Lachain.Proto.PingReply;

namespace Lachain.Networking
{
    public class PeerManager: IPeerManager
    {
        private static readonly ICrypto Crypto = CryptoProvider.GetCrypto();

        private static readonly ILogger<NetworkManagerBase> Logger =
            LoggerFactory.GetLoggerForClass<NetworkManagerBase>();
        
        private IMessageFactory? MessageFactory { get; set; }

        private readonly IPeerRepository _peerRepository;
        // private readonly ECDSAPublicKey _localPubKey;
        private INetworkBroadcaster _broadcaster;

        public PeerManager(IPeerRepository peerRepository)
        {
            _peerRepository = peerRepository;
            // _localPubKey = keyPair.PublicKey;
        }
        
        public List<PeerAddress> Start(NetworkConfig networkConfig, INetworkBroadcaster broadcaster, EcdsaKeyPair keyPair)
        {
            _broadcaster = broadcaster;
            MessageFactory = new MessageFactory(keyPair);
            string[] peers;
            var storedPeersCount = _peerRepository.GetPeersCount();
            if (storedPeersCount == 0 || networkConfig?.Peers != null)
            {
                peers = networkConfig?.Peers ?? throw new ArgumentException("Empty peer set");
                
                /* fill storage with initial peers */
                foreach (var peerStr in peers)
                {
                    var peerAddress = PeerAddress.Parse(peerStr);
                    var peer = new Peer
                    {
                        Host = peerAddress.Host,
                        Port = (uint)peerAddress.Port,
                        Timestamp = 0,
                    };
                    
                    _peerRepository.AddOrUpdatePeer(peerAddress.PublicKey!, peer);
                }
            }

            var currentPeer = new Peer
            {
                Host = GetExternalIp(),
                Port = networkConfig!.Port,
                Timestamp = (uint) (TimeUtils.CurrentTimeMillis() / 1000),
            };
            
            _peerRepository.AddOrUpdatePeer(keyPair.PublicKey, currentPeer);

            return GetSavedPeers();
        }

        public void UpdatePeerTimestamp(ECDSAPublicKey publicKey)
        {
            _peerRepository.UpdatePeerTimestampIfExist(publicKey);
        }

        // private void _HandleMessage(object sender, byte[] buffer)
        // {
        //     MessageBatch? batch;
        //     try
        //     {
        //         batch = MessageBatch.Parser.ParseFrom(buffer);
        //     }
        //     catch (Exception e)
        //     {
        //         Logger.LogError($"Unable to parse protocol message: {e}");
        //         Logger.LogTrace($"Original message bytes: {buffer.ToHex()}");
        //         return;
        //     }
        //
        //     if (batch is null)
        //     {
        //         Logger.LogError("Unable to parse protocol message");
        //         Logger.LogTrace($"Original message bytes: {buffer.ToHex()}");
        //         return;
        //     }
        //
        //     var publicKey = Crypto.RecoverSignature(batch.Content.ToArray(), batch.Signature.Encode())
        //         .ToPublicKey();
        //     var envelope = new MessageEnvelope
        //     {
        //         MessageFactory = MessageFactory,
        //         PublicKey = publicKey,
        //         RemotePeer = GetPeerByPublicKey(publicKey)
        //     };
        //     if (envelope.RemotePeer is null)
        //     {
        //         Logger.LogWarning(
        //             $"Message from unrecognized peer {publicKey.ToHex()} or invalid signature, skipping");
        //         return;
        //     }
        //
        //     GetPeerByPublicKey(envelope.PublicKey)?.Send(MessageFactory.Ack(batch.MessageId));
        //     var content = MessageBatchContent.Parser.ParseFrom(batch.Content);
        //     foreach (var message in content.Messages)
        //     {
        //         try
        //         {
        //             HandleMessageUnsafe(message, envelope);
        //         }
        //         catch (Exception e)
        //         {
        //             Logger.LogError($"Unexpected error occurred: {e}");
        //         }
        //     }
        // }

        // private Action<IMessage> SendTo(IRemotePeer? peer)
        // {
        //     return x =>
        //     {
        //         Logger.LogTrace($"Sending {x.GetType()} to {peer?.Address}");
        //         NetworkMessage msg = x switch
        //         {
        //             PingReply pingReply => new NetworkMessage {PingReply = pingReply},
        //             GetBlocksByHashesReply getBlocksByHashesReply => new NetworkMessage
        //             {
        //                 GetBlocksByHashesReply = getBlocksByHashesReply
        //             },
        //             GetBlocksByHeightRangeReply getBlocksByHeightRangeReply => new NetworkMessage
        //             {
        //                 GetBlocksByHeightRangeReply = getBlocksByHeightRangeReply
        //             },
        //             GetBlocksByHashesRequest getBlocksByHashesRequest => new NetworkMessage
        //             {
        //                 GetBlocksByHashesRequest = getBlocksByHashesRequest
        //             },
        //             GetTransactionsByHashesReply getTransactionsByHashesReply => new NetworkMessage
        //             {
        //                 GetTransactionsByHashesReply = getTransactionsByHashesReply
        //             },
        //             _ => throw new InvalidOperationException()
        //         };
        //         peer?.Send(msg);
        //     };
        // }

        // private void HandleMessageUnsafe(NetworkMessage message, MessageEnvelope envelope)
        // {
        //     if (envelope.PublicKey is null) throw new InvalidOperationException();
        //     switch (message.MessageCase)
        //     {
        //         case NetworkMessage.MessageOneofCase.HandshakeRequest:
        //             _HandshakeRequest(message.HandshakeRequest);
        //             break;
        //         case NetworkMessage.MessageOneofCase.HandshakeReply:
        //             _HandshakeReply(message.HandshakeReply);
        //             break;
        //         case NetworkMessage.MessageOneofCase.PingRequest:
        //             OnPingRequest?.Invoke(this, (message.PingRequest, SendTo(envelope.RemotePeer)));
        //             break;
        //         case NetworkMessage.MessageOneofCase.PingReply:
        //             OnPingReply?.Invoke(this, (message.PingReply, envelope.PublicKey));
        //             break;
        //         case NetworkMessage.MessageOneofCase.GetBlocksByHashesRequest:
        //             OnGetBlocksByHashesRequest?.Invoke(this,
        //                 (message.GetBlocksByHashesRequest, SendTo(envelope.RemotePeer))
        //             );
        //             break;
        //         case NetworkMessage.MessageOneofCase.GetBlocksByHashesReply:
        //             OnGetBlocksByHashesReply?.Invoke(this, (message.GetBlocksByHashesReply, envelope.PublicKey));
        //             break;
        //         case NetworkMessage.MessageOneofCase.GetBlocksByHeightRangeRequest:
        //             OnGetBlocksByHeightRangeRequest?.Invoke(this,
        //                 (message.GetBlocksByHeightRangeRequest, SendTo(envelope.RemotePeer))
        //             );
        //             break;
        //         case NetworkMessage.MessageOneofCase.GetBlocksByHeightRangeReply:
        //             OnGetBlocksByHeightRangeReply?.Invoke(this,
        //                 (message.GetBlocksByHeightRangeReply, SendTo(envelope.RemotePeer))
        //             );
        //             break;
        //         case NetworkMessage.MessageOneofCase.GetTransactionsByHashesRequest:
        //             OnGetTransactionsByHashesRequest?.Invoke(this,
        //                 (message.GetTransactionsByHashesRequest, SendTo(envelope.RemotePeer))
        //             );
        //             break;
        //         case NetworkMessage.MessageOneofCase.GetTransactionsByHashesReply:
        //             OnGetTransactionsByHashesReply?.Invoke(this,
        //                 (message.GetTransactionsByHashesReply, envelope.PublicKey)
        //             );
        //             break;
        //         case NetworkMessage.MessageOneofCase.Ack:
        //             GetPeerByPublicKey(envelope.PublicKey)?.ReceiveAck(message.Ack.MessageId);
        //             break;
        //         default:
        //             throw new ArgumentOutOfRangeException(nameof(message),
        //                 "Unable to resolve message type (" + message.MessageCase + ") from protobuf structure");
        //     }
        // }

        // private void _ConnectWorker()
        // {
        //     var thread = Thread.CurrentThread;
        //     while (thread.IsAlive)
        //     {
        //         lock (_hasPeersToConnect)
        //         {
        //             Monitor.Wait(_hasPeersToConnect, TimeSpan.FromSeconds(5));
        //             if (!_clientWorkers.Any())
        //                 continue;
        //             foreach (var address in _clientWorkers.Keys)
        //             {
        //                 try
        //                 {
        //                     Logger.LogTrace($"Sending handshake request to {address}");
        //                     ClientWorker.SendOnce(
        //                         address,
        //                         MessageFactory.MessagesBatch(new[] {MessageFactory.HandshakeRequest(LocalNode)})
        //                     );
        //                 }
        //                 catch (Exception e)
        //                 {
        //                     Logger.LogError($"Failed to connect to node {address}: {e}");
        //                 }
        //             }
        //         }
        //     }
        // }

        public void BroadcastGetPeersRequest()
        {
            Logger.LogInformation("Broadcasting get peer request");
            var message = MessageFactory?.GetPeersRequest() ??
                          throw new InvalidOperationException();
            _broadcaster.Broadcast(message);
        }

        // public void BroadcastGetPeersReply()
        // {
        //     var (peers, publicKeys) = GetPeersToBroadcast();
        //     
        //     var message = MessageFactory?.GetPeersReply(peers, publicKeys) ?? throw new InvalidOperationException();
        //     _broadcaster.Broadcast(message);
        // }

        public (Peer[], ECDSAPublicKey[]) GetPeersToBroadcast()
        {
            var publicKeys = _peerRepository.GetPeerList().ToArray();

            var peers = publicKeys.Select(pub =>
                    _peerRepository.GetPeerByPublicKey(pub)!)
                .Where(peer => peer.Timestamp > TimeUtils.CurrentTimeMillis() / 1000 - 3 * 3600)
                .ToArray();
            return (peers, publicKeys);
        }
        //
        // public void Stop()
        // {
        //     _serverWorker?.Stop();
        // }

        // public void SendTo(ECDSAPublicKey publicKey, NetworkMessage networkMessage)
        // {
        //     ConsensusNetworkManager.SendTo(publicKey, networkMessage);
        // }

        private static string GetExternalIp()
        {
            Random rnd = new Random();
            var ipResolverUrls = new[]
            {
                "http://icanhazip.com",
                "https://api.ipify.org",
                "https://ip.seeip.org/",
                "http://bot.whatismyipaddress.com/",
                "http://ipv4bot.whatismyipaddress.com/",
                "http://ipinfo.io/ip",
                "https://www.trackip.net/ip",
                "https://ifconfig.me/ip",
            }.OrderBy(x => rnd.Next()).ToArray();
            string? ip = null;
            foreach (var url in ipResolverUrls)
            {
                try
                {
                    ip = new WebClient().DownloadString(url);
                    break;
                }
                catch (WebException e)
                {
                    Logger.LogError($"Can't access {url}");
                }
            }
            if (ip == null) throw new Exception("Cannot resolve external node IP");
            Logger.LogInformation($"My ip: {ip}");
            return ip;
        }
        
        public static PeerAddress ToPeerAddress(Peer peer, ECDSAPublicKey pub)
        {
            var peerAddrStr = $"tcp://{pub.EncodeCompressed().ToHex(false)}@{peer.Host}:{peer.Port}";
            return PeerAddress.Parse(peerAddrStr);
        }

        public List<PeerAddress> GetSavedPeers()
        {
            var storedPeers = _peerRepository.GetPeerList().ToArray();
            if (storedPeers.Length == 0)
                throw new Exception("There are no peers to load");

            return (from peerPubKey in storedPeers let peer = _peerRepository.GetPeerByPublicKey(peerPubKey)! select ToPeerAddress(peer, peerPubKey)).ToList();
        }
    }
}