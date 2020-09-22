using Newtonsoft.Json;

namespace Lachain.Networking
{
    public class NetworkConfig
    {
        [JsonProperty("port")] public ushort Port { get; set; }

        [JsonProperty("peers")] public string[]? Peers { get; set; }

        [JsonProperty("forceIPv6")] public bool ForceIPv6 { get; set; }

        [JsonProperty("maxPeers")] public ushort MaxPeers { get; set; }
        
        [JsonProperty("bootstrapAddresses")] public string[]? BootstrapAddresses { get; set; }
    }
}