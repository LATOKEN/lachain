using Newtonsoft.Json;

namespace Phorkus.Core.Network
{
    public class NetworkConfig
    {
        [JsonProperty("magic")]
        public uint Magic { get; set; }
        
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