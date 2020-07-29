using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
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
        private INetworkBroadcaster? _broadcaster;

        private readonly ulong _peerCheckTimeout = 1 * 60;
        private bool _started = false;
        private Thread? _worker;
        private NetworkConfig? _networkConfig;

        public PeerManager(IPeerRepository peerRepository)
        {
            _peerRepository = peerRepository;
            // _localPubKey = keyPair.PublicKey;
        }
        
        [MethodImpl(MethodImplOptions.Synchronized)]
        public List<PeerAddress> Start(NetworkConfig networkConfig, INetworkBroadcaster broadcaster, EcdsaKeyPair keyPair)
        {
            _broadcaster = broadcaster;
            _networkConfig = networkConfig;
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
                        Host = NetworkManagerBase.CheckLocalConnection(peerAddress.Host!),
                        Port = (uint)peerAddress.Port,
                        Timestamp = 0,
                    };
                    
                    _peerRepository.AddOrUpdatePeer(peerAddress.PublicKey!, peer);
                }
            }
            
            _peerRepository.AddOrUpdatePeer(keyPair.PublicKey, GetCurrentPeer());
            
            StartCheckingWorker();

            return GetSavedPeers();
        }
        
        [MethodImpl(MethodImplOptions.Synchronized)]
        private void Run()
        {
            try
            {
                Logger.LogInformation($"Peer manager started");

                while (_started)
                {
                    Thread.Sleep(TimeSpan.FromSeconds(_peerCheckTimeout));
                    BroadcastGetPeersRequest();
                }
            }
            catch (ThreadInterruptedException e)
            {
                Logger.LogDebug($"Interrupt received, exiting: {e}");
            }
            catch (Exception e)
            {
                Logger.LogCritical($"Fatal error in peer manager, exiting: {e}");
                Environment.Exit(1);
            }
        }
        
        
        public void StartCheckingWorker()
        {
            if (_started)
            {
                Logger.LogWarning("Service already started");
                return;
            }

            _started = true;
            _worker = new Thread(Run);
            _worker.Start();
        }

        public void Stop()
        {
            _worker?.Interrupt();
        }

        public void UpdatePeerTimestamp(ECDSAPublicKey publicKey)
        {
            _peerRepository.UpdatePeerTimestampIfExist(publicKey);
        }

        public void AddPeer(Peer peer, ECDSAPublicKey publicKey)
        {
            _peerRepository.AddOrUpdatePeer(publicKey, peer);
        }

        public Peer GetCurrentPeer()
        {
            return new Peer
            {
                Host = GetExternalIp(),
                Port = _networkConfig!.Port,
                Timestamp = (uint) (TimeUtils.CurrentTimeMillis() / 1000),
            };
        }

        public void BroadcastGetPeersRequest()
        {
            Logger.LogInformation("Broadcasting getPeersRequest");
            var message = MessageFactory?.GetPeersRequest() ??
                          throw new InvalidOperationException();
            _broadcaster!.Broadcast(message);
        }

        public void BroadcastPeerJoin()
        {
            var message = MessageFactory?.PeerJoin(GetCurrentPeer()) ??
                          throw new InvalidOperationException();
            Logger.LogInformation("Broadcasting PeerJoin");
            _broadcaster!.Broadcast(message);
        }

        public (Peer[], ECDSAPublicKey[]) GetPeersToBroadcast()
        {
            var allPublicKeys = _peerRepository.GetPeerList().ToArray();
            var publicKeysToBroadcast = new List<ECDSAPublicKey>();

            var peers = allPublicKeys.Select(pub => 
                    _peerRepository.GetPeerByPublicKey(pub)!)
                .Where((peer, i) =>
                {
                    if (peer.Timestamp > TimeUtils.CurrentTimeMillis() / 1000 - 3 * 3600)
                    {
                        publicKeysToBroadcast.Add(allPublicKeys[i]);
                        return true;
                    }

                    return false;
                })
                .ToArray();
            return (peers, publicKeysToBroadcast.ToArray());
        }
        
        public List<PeerAddress> HandlePeersFromPeer(IEnumerable<Peer> peers, IEnumerable<ECDSAPublicKey> publicKeys)
        {
            var peerArray = peers as Peer[] ?? peers.ToArray();
            foreach (var peer in peerArray)
            {
                peer.Host = NetworkManagerBase.CheckLocalConnection(peer.Host);
            }
            var newPeers = new List<PeerAddress>();
            var keys = publicKeys as ECDSAPublicKey[] ?? publicKeys.ToArray();
            if (peerArray.Length != keys.Length) 
                throw new ArgumentException($"peers.Length: {peerArray.Length} != keys.Length: {keys.Length}");
            
            for (var i = 0; i < peerArray.Length; i++)
            {
                if (!_peerRepository.ContainsPublicKey(keys[i]))
                    newPeers.Add(ToPeerAddress(peerArray[i], keys[i]));
                _peerRepository.AddOrUpdatePeer(keys[i], peerArray[i]);
            }

            return newPeers;
        }

        public static string GetExternalIp()
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
                    var input = new WebClient().DownloadString(url);
                    Regex ipRg = new Regex(@"\b\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}\b");
                    MatchCollection result = ipRg.Matches(input);
                    ip = result[0].ToString();
                    break;
                }
                catch (Exception e)
                {
                    Logger.LogError($"Unable to resolve ip via {url}: {ip}");
                }
            }
            if (ip == null) 
                throw new Exception("Cannot resolve external node IP");
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