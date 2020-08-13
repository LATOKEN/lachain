using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Numerics;
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
using Lachain.Storage.State;
using Lachain.Utility.Utils;
using Nethereum.Util;
using NLog.Fluent;
using PingReply = Lachain.Proto.PingReply;

namespace Lachain.Networking
{
    public class PeerManager: IPeerManager
    {
        private static readonly ILogger<NetworkManagerBase> Logger =
            LoggerFactory.GetLoggerForClass<NetworkManagerBase>();
        
        private IMessageFactory? MessageFactory { get; set; }

        private readonly IPeerRepository _peerRepository;
        private INetworkBroadcaster? _broadcaster;
        private Dictionary<PeerAddress, ulong> _lastRequest = new Dictionary<PeerAddress, ulong>();

        private static ulong PeerRequestTimeout { get; } = 15 * 60;

        private static readonly ulong CleanUpTick = 5 * 60 * 60 / PeerRequestTimeout + 1;
        private bool _started;
        private string? _externalIp;
        private ulong _externalIpCheckTimeout = 15 * 60;
        private ulong _externalIpLastCheckTime;
        private Thread? _worker;
        private NetworkConfig? _networkConfig;
        private readonly IStateManager _stateManager;

        public PeerManager(IPeerRepository peerRepository, IStateManager stateManager)
        {
            _peerRepository = peerRepository;
            _stateManager = stateManager;
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
                        Host = CheckLocalConnection(peerAddress.Host!),
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

        public ECDSAPublicKey[] GetStakers()
        {
            var stakersData = _stateManager.LastApprovedSnapshot.Storage.GetRawValue(
                new BigInteger(3).ToUInt160(),
                new BigInteger(6).ToUInt256().ToBytes());
            var stakers = new List<ECDSAPublicKey> ();
            for (var i = CryptoUtils.PublicKeyLength; i < stakersData.Length; i += CryptoUtils.PublicKeyLength)
            {
                var staker = stakersData.Slice(i, i + CryptoUtils.PublicKeyLength).ToPublicKey();
                stakers.Add(staker);
            }

            return stakers.ToArray();
        }
        
        [MethodImpl(MethodImplOptions.Synchronized)]
        private void Run()
        {
            var ticks = (ulong) 1; 
            try
            {
                Logger.LogInformation($"Peer manager started");

                while (_started)
                {
                    Thread.Sleep(TimeSpan.FromSeconds(PeerRequestTimeout));
                    if (ticks == CleanUpTick)
                    {
                        PeerCleanUp();
                        ticks = 1;
                    }
                    else
                        ticks++;
                    
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

        public PeerAddress? GetPeerAddressByPublicKey(ECDSAPublicKey key)
        {
            var peer = _peerRepository.GetPeerByPublicKey(key);
            if (peer == null) return null;
            return ToPeerAddress(peer, key);
        }

        public Peer? GetPeerByPublicKey(ECDSAPublicKey key)
        {
            return _peerRepository.GetPeerByPublicKey(key);
        }

        public List<PeerAddress> GetPeerAddressesByPublicKeys(IEnumerable<ECDSAPublicKey> keys)
        {
            var result = new List<PeerAddress>();
            foreach (var key in keys)
            {
                var peerAddr = GetPeerAddressByPublicKey(key);
                if (peerAddr!= null) result.Add(peerAddr);
            }

            return result;
        }

        public void BroadcastGetPeersRequest()
        {
            Logger.LogInformation("Broadcasting getPeersRequest");
            var message = MessageFactory?.GetPeersRequest() ??
                          throw new InvalidOperationException();
            _broadcaster!.Broadcast(message);
        }

        public void BroadcastPeerJoinRequest()
        {
            var message = MessageFactory?.PeerJoinRequest(GetCurrentPeer()) ??
                          throw new InvalidOperationException();
            Logger.LogInformation("Broadcasting PeerJoinRequest");
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

        private void PeerCleanUp()
        {
            var allPublicKeys = _peerRepository.GetPeerList().ToArray();
            foreach (var publicKey in allPublicKeys)
            {
                var peer = _peerRepository.GetPeerByPublicKey(publicKey)!;
                var peerAddress = ToPeerAddress(peer, publicKey);
                if (peer.Timestamp >= TimeUtils.CurrentTimeMillis() / 1000 - 6 * 24 * 3600) continue;
                Logger.LogDebug($"Removing old non-active peer {peerAddress}");
                _peerRepository.RemovePeer(publicKey);
            }
        }
        
        public List<PeerAddress> HandlePeersFromPeer(IEnumerable<Peer> peers, IEnumerable<ECDSAPublicKey> publicKeys)
        {
            var peerArray = peers as Peer[] ?? peers.ToArray();
            foreach (var peer in peerArray)
            {
                peer.Host = CheckLocalConnection(peer.Host);
            }
            var peerAddresses = new List<PeerAddress>();
            var keys = publicKeys as ECDSAPublicKey[] ?? publicKeys.ToArray();
            if (peerArray.Length != keys.Length) 
                throw new ArgumentException($"peers.Length: {peerArray.Length} != keys.Length: {keys.Length}");
            
            for (var i = 0; i < peerArray.Length; i++)
            {
                
                peerAddresses.Add(ToPeerAddress(peerArray[i], keys[i]));
                _peerRepository.AddOrUpdatePeer(keys[i], peerArray[i]);
            }

            return peerAddresses;
        }

        public string GetExternalIp()
        {
            var shouldUpdateIp = (ulong) DateTimeOffset.UtcNow.ToUnixTimeSeconds() >
                                 _externalIpLastCheckTime + _externalIpCheckTimeout;
            if (_externalIp == null || shouldUpdateIp) DetectExternalIp();
            return _externalIp!;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        private void DetectExternalIp()
        {
            Random rnd = new Random();
            var ipResolverUrls = new[]
            {
                "http://checkip.dyndns.org",
                "https://www.showmyip.com",
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
                    Logger.LogError($"Unable to resolve ip via {url}: {e}");
                }
            }

            _externalIp = ip ?? throw new Exception("Cannot resolve node external IP");
            _externalIpLastCheckTime = (ulong) DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }

        // If a node has an IP that matches with our own IP,
        // save & establish this connection via localhost
        public string CheckLocalConnection(string host)
        {
            // TODO: this is hack
            // Here should be code to determine correct path to node 
            return IPAddress.Parse(host).Equals(IPAddress.Parse(GetExternalIp())) ? new IPAddress(0x0100007f).ToString() : host;
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