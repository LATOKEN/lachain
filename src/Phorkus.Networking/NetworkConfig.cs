using Newtonsoft.Json;

namespace Phorkus.Networking
{
    public class NetworkConfig
    {
        [JsonProperty("magic")]
        public uint Magic { get; set; }

        [JsonProperty("address")]
        public string Address { get; set; } = "localhost";

        [JsonProperty("port")]
        public ushort Port { get; set; }
        
        [JsonProperty("peers")]
        public string[] Peers { get; set; }
        
        [JsonProperty("forceIPv6")]
        public bool ForceIPv6 { get; set; }
        
        [JsonProperty("maxPeers")]
        public ushort MaxPeers { get; set; }
    }
}