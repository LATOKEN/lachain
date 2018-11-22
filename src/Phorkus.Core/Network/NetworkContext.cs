using System;
using System.Collections.Concurrent;
using Phorkus.Core.Config;
using Phorkus.Proto;

namespace Phorkus.Core.Network
{
    class NetworkContext : INetworkContext
    {
        public NetworkContext(IConfigManager configManager)
        {
            var networkConfig = configManager.GetConfig<NetworkConfig>("network");
            LocalNode = new Node
            {
                Version = 0,
                Timestamp = (ulong) DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Services = 0,
                Port = networkConfig.Port,
                Address = "localhost",
                Nonce = (uint) new Random().Next(1 << 30),
                BlockHeight = 0,
                Agent = "Phorkus-v0.0"
            };
        }

        public Node LocalNode { get; }

        public ConcurrentDictionary<IpEndPoint, IPeer> ActivePeers { get; }
            = new ConcurrentDictionary<IpEndPoint, IPeer>();
    }
}