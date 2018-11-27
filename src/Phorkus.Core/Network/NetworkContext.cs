using System;
using System.Collections.Concurrent;
using Phorkus.Core.Blockchain;
using Phorkus.Core.Config;
using Phorkus.Proto;

namespace Phorkus.Core.Network
{
    public class NetworkContext : INetworkContext
    {
        public ConcurrentDictionary<PeerAddress, IRemotePeer> ActivePeers { get; }
            = new ConcurrentDictionary<PeerAddress, IRemotePeer>();

        private readonly IBlockchainContext _blockchainContext;
        private readonly NetworkConfig _networkConfig;
        
        public Node LocalNode => new Node
        {
            Version = 0,
            Timestamp = (ulong) DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            Services = 0,
            Port = _networkConfig.Port,
            Address = "localhost",
            Nonce = (uint) new Random().Next(1 << 30),
            BlockHeight = _blockchainContext.CurrentBlockHeaderHeight,
            Agent = "Phorkus-v0.0"
        };
        
        public NetworkContext(IConfigManager configManager, IBlockchainContext blockchainContext)
        {
            _blockchainContext = blockchainContext;
            _networkConfig = configManager.GetConfig<NetworkConfig>("network");
        }
    }
}