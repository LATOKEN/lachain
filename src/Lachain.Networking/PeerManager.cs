using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using Lachain.Crypto;
using Lachain.Logger;
using Lachain.Proto;
using Lachain.Storage.Repositories;
using Lachain.Utility.Utils;

namespace Lachain.Networking
{
    public class PeerManager : IPeerManager
    {
        private static readonly ILogger<NetworkManagerBase> Logger =
            LoggerFactory.GetLoggerForClass<NetworkManagerBase>();

        private const ulong PeerRequestTimeout = 15 * 60;
        private const ulong CleanUpTick = 5 * 60 * 60 / PeerRequestTimeout + 1;

        private readonly IMessageFactory _messageFactory;
        private readonly NetworkConfig _networkConfig;
        private readonly IPeerRepository _peerRepository;
        private readonly INetworkBroadcaster _broadcaster;

        private bool _started;
        private Thread _worker;

        public PeerManager(
            IPeerRepository peerRepository, INetworkBroadcaster broadcaster,
            IMessageFactory messageFactory, NetworkConfig networkConfig
        )
        {
            _peerRepository = peerRepository;
            _broadcaster = broadcaster;
            _messageFactory = messageFactory;
            _networkConfig = networkConfig;
            _worker = new Thread(Run);
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public List<Peer> Start()
        {
            var storedPeersCount = _peerRepository.GetPeersCount();
            if (storedPeersCount == 0 || _networkConfig.Peers != null)
            {
                string[] peers = _networkConfig.Peers ?? throw new ArgumentException("Empty peer set");

                /* fill storage with initial peers */
                foreach (var peerStr in peers)
                {
                    var peerPublicKey = peerStr.HexToBytes().ToPublicKey();
                    var peer = new Peer {Timestamp = 0, PublicKey = peerPublicKey};
                    _peerRepository.AddOrUpdatePeer(peerPublicKey, peer);
                }
            }

            StartCheckingWorker();
            return GetSavedPeers();
        }

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

        private void StartCheckingWorker()
        {
            if (_started)
            {
                Logger.LogWarning("Service already started");
                return;
            }

            _started = true;
            _worker.Start();
        }

        public void Stop()
        {
            if (!_started) return;
            _worker.Interrupt();
            _worker.Join();
        }

        public void UpdatePeerTimestamp(ECDSAPublicKey publicKey)
        {
            _peerRepository.UpdatePeerTimestampIfExist(publicKey);
        }

        public void BroadcastGetPeersRequest()
        {
            Logger.LogDebug("Broadcasting getPeersRequest");
            var message = _messageFactory.GetPeersRequest();
            _broadcaster.Broadcast(message);
        }

        public Peer[] GetPeersToBroadcast()
        {
            return GetSavedPeers()
                .Where(peer => peer.Timestamp.ToDateTime() > DateTimeOffset.Now.Subtract(TimeSpan.FromHours(3)))
                .ToArray();
        }

        private void PeerCleanUp()
        {
            var allPublicKeys = _peerRepository.GetPeerList().ToArray();
            foreach (var publicKey in allPublicKeys)
            {
                var peer = _peerRepository.GetPeerByPublicKey(publicKey)!;
                if (peer.Timestamp.ToDateTime() >= DateTimeOffset.Now.Subtract(TimeSpan.FromHours(6))) continue;
                Logger.LogTrace($"Removing old non-active peer {peer.PublicKey.ToHex()}");
                _peerRepository.RemovePeer(publicKey);
            }
        }

        public IEnumerable<Peer> HandlePeersFromPeer(IEnumerable<Peer> peers)
        {
            var peerArray = peers as Peer[] ?? peers.ToArray();
            foreach (var t in peerArray)
                _peerRepository.AddOrUpdatePeer(t.PublicKey, t);
            return peerArray;
        }

        private List<Peer> GetSavedPeers()
        {
            return _peerRepository.GetPeerList()
                .Select(key => _peerRepository.GetPeerByPublicKey(key))
                .Where(peer => peer != null)
                .Select(peer => peer!)
                .ToList();
        }

        public void Dispose()
        {
            Stop();
        }
    }
}