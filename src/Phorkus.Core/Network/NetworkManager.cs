using System;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Timers;
using Grpc.Core;
using Phorkus.Core.Blockchain;
using Phorkus.Core.Blockchain.OperationManager;
using Phorkus.Core.Config;
using Phorkus.Core.Consensus;
using Phorkus.Core.Network.Grpc;
using Phorkus.Core.Storage;
using Phorkus.Core.Threshold;
using Phorkus.Crypto;
using Phorkus.Network.Grpc;
using Phorkus.Proto;
using Phorkus.Utility.Utils;

namespace Phorkus.Core.Network
{
    public class NetworkManager : INetworkManager
    {
        private readonly INetworkContext _networkContext;

        public event EventHandler<IRemotePeer> OnPeerConnected;
        public event EventHandler<IRemotePeer> OnPeerClosed;

        private class RemotePeer : IRemotePeer
        {
            public bool IsConnected { get; set; }
            public bool IsKnown { get; set; }
            public PeerAddress Address { get; set; }
            public Node Node { get; set; }
            public IRateLimiter RateLimiter { get; set; }
            public DateTime Connected { get; set; }
            public IBlockchainService BlockchainService { get; set; }
            public IConsensusService ConsensusService { get; set; }
            public IThresholdService ThresholdService { get; set; }
        }

        public NetworkManager(
            IConfigManager configManager,
            ITransactionRepository transactionRepository,
            IBlockRepository blockRepository,
            IBlockSynchronizer blockSynchronizer,
            IBlockManager blockManager,
            IThresholdManager thresholdManager,
            INetworkContext networkContext,
            IConsensusManager consensusManager,
            IValidatorManager validatorManager,
            ICrypto crypto)
        {
            var networkConfig = configManager.GetConfig<NetworkConfig>("network");

            var server = new Server
            {
                Services =
                {
                    ThresholdService.BindService(new GrpcThresholdServiceServer(thresholdManager, crypto)),
                    BlockchainService.BindService(new GrpcBlockchainServiceServer(networkContext, transactionRepository,
                        blockRepository, blockSynchronizer)),
                    ConsensusService.BindService(new GrpcConsensusServiceServer(consensusManager, crypto, validatorManager))
                },
                Ports = {new ServerPort("0.0.0.0", networkConfig.Port, ServerCredentials.Insecure)}
            };
            server.Start();

            var timer = new Timer();
            timer.Elapsed += (o, e) =>
            {
                foreach (var peer in networkConfig.Peers)
                {
                    var peerAddress = PeerAddress.Parse(peer);
                    if (networkContext.ActivePeers.ContainsKey(peerAddress))
                        continue;
                    var remotePeer = new RemotePeer
                    {
                        IsConnected = true,
                        IsKnown = false,
                        Address = peerAddress,
                        Node = new Node(),
                        RateLimiter = new NullRateLimiter(),
                        Connected = DateTime.Now,
                        BlockchainService = new GrpcBlockchainServiceClient(peerAddress),
                        ConsensusService = new GrpcConsensusServiceClient(peerAddress, crypto),
                        ThresholdService = new GrpcThresholdServiceClient(peerAddress, crypto)
                    };
                    if (_IsNodeAvailable(networkContext, remotePeer))
                        networkContext.ActivePeers.TryAdd(peerAddress, remotePeer);
                    else
                        networkContext.ActivePeers.TryRemove(peerAddress, out _);
                }
            };
            timer.Interval = 1000;
            timer.Start();

            blockManager.OnBlockPersisted += _OnBlockPersisted;

            _networkContext = networkContext;
        }

        private void _OnBlockPersisted(object sender, Block block)
        {
            var handshake = new HandshakeRequest
            {
                Node = _networkContext.LocalNode
            };
            foreach (var peer in _networkContext.ActivePeers.Values)
                peer.BlockchainService.Handshake(handshake);
        }

        private static bool _IsSelfConnect(PeerAddress ipAddress)
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            var hosts = host.AddressList.Select(ad => ad.ToString()).ToArray();
            if (hosts.Contains(ipAddress.Host))
                return true;
            var networkInterfaces = NetworkInterface.GetAllNetworkInterfaces();
            foreach (var ni in networkInterfaces)
            {
                if (ni.NetworkInterfaceType != NetworkInterfaceType.Wireless80211 &&
                    ni.NetworkInterfaceType != NetworkInterfaceType.Ethernet)
                {
                    continue;
                }

                foreach (var ip in ni.GetIPProperties().UnicastAddresses)
                {
                    if (ip.Address.AddressFamily != AddressFamily.InterNetwork)
                        continue;
                    if (ip.Address.ToString().Contains(ipAddress.Host))
                        continue;
                    return true;
                }
            }

            return false;
        }

        private bool _IsNodeAvailable(INetworkContext networkContext, IRemotePeer remotePeer)
        {
            try
            {
//                if (_IsSelfConnect(remotePeer.Address))
//                    return false;
                var ping = new PingRequest
                {
                    Timestamp = TimeUtils.CurrentTimeMillis()
                };
                var pong = remotePeer.BlockchainService.Ping(ping);
                if (pong.Timestamp != ping.Timestamp)
                    return false;

                var handshake = new HandshakeRequest
                {
                    Node = networkContext.LocalNode
                };
                var reply = remotePeer.BlockchainService.Handshake(handshake);
                if (reply.Node == null || !_IsValid(reply.Node))
                    return false;
                remotePeer.Node = reply.Node;
                remotePeer.IsKnown = true;
            }
            catch (Exception)
            {
                return false;
            }

            return true;
        }
        
        private static bool _IsValid(Node node)
        {
            if (string.IsNullOrEmpty(node.Address))
                return false;
            if (string.IsNullOrEmpty(node.Agent))
                return false;
            return node.Nonce != 0;
        }

        public void Start()
        {
        }

        public void Stop()
        {
        }
    }
}