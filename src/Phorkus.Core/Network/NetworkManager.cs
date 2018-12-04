using System;
using System.Threading.Tasks;
using Grpc.Core;
using Phorkus.Core.Blockchain.OperationManager;
using Phorkus.Core.Config;
using Phorkus.Core.Network.Grpc;
using Phorkus.Core.Storage;
using Phorkus.Core.Threshold;
using Phorkus.Core.Utils;
using Phorkus.Crypto;
using Phorkus.Network.Grpc;
using Phorkus.Proto;

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
            ICrypto crypto,
            IThresholdManager thresholdManager,
            INetworkContext networkContext)
        {
            var networkConfig = configManager.GetConfig<NetworkConfig>("network");

            var server = new Server
            {
                Services =
                {
                    ThresholdService.BindService(new GrpcThresholdServiceServer(thresholdManager, crypto)),
                    BlockchainService.BindService(new GrpcBlockchainServiceServer(networkContext, transactionRepository, blockRepository, blockSynchronizer)),
                    ConsensusService.BindService(new GrpcConsensusServiceServer(null, crypto))
                },
                Ports = { new ServerPort("0.0.0.0", networkConfig.Port, ServerCredentials.Insecure) }
            };
            server.Start();
            
            Parallel.ForEach(networkConfig.Peers, address =>
            {
                var peerAddress = PeerAddress.Parse(address);
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
            });
            
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
        
        private bool _IsNodeAvailable(INetworkContext networkContext, IRemotePeer remotePeer)
        {
            try
            {
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
                if (reply.Node == null || !reply.Node.IsValid())
                    return false;
                remotePeer.Node = reply.Node;
                remotePeer.IsKnown = true;
            }
            catch (Exception e)
            {
                Console.Error.WriteLine(e.Message);
                return false;
            }

            return true;
        }
        
        public void Start()
        {
        }

        public void Stop()
        {
        }
    }
}