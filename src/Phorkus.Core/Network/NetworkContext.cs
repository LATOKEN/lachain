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
        private readonly IValidatorManager _validatorManager;
        
        public Node LocalNode => new Node
        {
            Version = 0,
            Timestamp = (ulong) DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            Address = $"tcp://192.168.88.154:{_networkConfig.Port}",
            Nonce = (uint) new Random().Next(1 << 30),
            BlockHeight = _blockchainContext.CurrentBlockHeaderHeight,
            Agent = "Phorkus-v0.0-dev"
        };
        
        public NetworkContext(
            IBlockchainContext blockchainContext,
            IConfigManager configManager,
            IValidatorManager validatorManager)
        {
            _blockchainContext = blockchainContext;
            _validatorManager = validatorManager;
            _networkConfig = configManager.GetConfig<NetworkConfig>("network");
        }
        
        public IRemotePeer GetPeerByPublicKey(PublicKey publicKey)
        {
            var validatorIndex = _validatorManager.GetValidatorIndex(publicKey);
            if (_networkConfig.Peers.Length <= validatorIndex)
                return null;
            var address = PeerAddress.Parse(_networkConfig.Peers[validatorIndex]);
            if (!ActivePeers.ContainsKey(address))
                return null;
            return ActivePeers[address];
        }
    }
}