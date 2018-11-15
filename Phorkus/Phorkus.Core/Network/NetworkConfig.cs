using Microsoft.Extensions.Configuration;

namespace Phorkus.Core.Network
{
    public class NetworkConfig
    {
        /// <summary>
        /// Magic number
        /// </summary>
        public uint Magic { get; internal set; }
        /// <summary>
        /// Portt
        /// </summary>
        public ushort Port { get; internal set; }
        /// <summary>
        /// Force Ipv6
        /// </summary>
        public bool ForceIPv6 { get; internal set; }
        /// <summary>
        /// Peers
        /// </summary>
        public IpEndPoint[] PeerEndPoints { get; internal set; }
        /// <summary>
        /// Acl Config
        /// </summary>
        public AclConfig AclConfig { get; internal set; }
        /// <summary>
        /// StandByValidator config
        /// </summary>
        public string[] StandByValidator { get; internal set; }
        /// <summary>
        /// Max number of connected peers
        /// </summary>
        public ushort MaxConnectedPeers { get; internal set; } = 10;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="configuration">Configuration</param>
        public NetworkConfig(IConfiguration configuration)
        {
            PeerEndPoints = new IpEndPoint[0];
//            configuration?.GetSection("network")?.Bind(this);
        }
    }
}