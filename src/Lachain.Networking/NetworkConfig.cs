using Newtonsoft.Json;

namespace Lachain.Networking
{
    public class NetworkConfig
    {
        [JsonProperty("peers")] public string[]? Peers { get; set; }

        [JsonProperty("forceIPv6")] public bool ForceIPv6 { get; set; }

        [JsonProperty("maxPeers")] public ushort MaxPeers { get; set; }
        
        [JsonProperty("bootstrapAddresses")] public string[]? BootstrapAddresses { get; set; }
        
        [JsonProperty("hubLogLevel")] public string? HubLogLevel { get; set; }
        
        [JsonProperty("hubMetricsPort")] public int? HubMetricsPort { get; set; }
        
        [JsonProperty("networkName")] public string? NetworkName { get; set; }
    }
}