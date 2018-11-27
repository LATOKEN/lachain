using System;
using System.Threading.Tasks;
using Grpc.Core;
using Phorkus.Core.Config;
using Phorkus.Core.Cryptography;
using Phorkus.Core.Network.Grpc;
using Phorkus.Core.Storage;
using Phorkus.Core.Storage.Repositories;
using Phorkus.Core.Utils;
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
        }
        
        public NetworkManager(
            IConfigManager configManager,
            ITransactionRepository transactionRepository,
            IBlockRepository blockRepository,
            ICrypto crypto,
            INetworkContext networkContext)
        {
            var networkConfig = configManager.GetConfig<NetworkConfig>("network");

            var server = new Server
            {
                Services =
                {
                    BlockchainService.BindService(new GrpcBlockchainServiceServer(networkContext, transactionRepository, blockRepository)),
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
                    ConsensusService = new GrpcConsensusServiceClient(peerAddress, crypto)
                };
                if (_IsNodeAvailable(networkContext, remotePeer))
                    networkContext.ActivePeers.TryAdd(peerAddress, remotePeer);
            });
            
            _networkContext = networkContext;
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