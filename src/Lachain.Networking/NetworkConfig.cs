using Newtonsoft.Json;

namespace Lachain.Networking
{
    public class NetworkConfig
    {
        [JsonProperty("address")] public string Address { get; set; } = "0.0.0.0";

        [JsonProperty("host")] public string? MyHost { get; set; }

        [JsonProperty("port")] public ushort Port { get; set; }

        [JsonProperty("peers")] public string[]? Peers { get; set; }

        [JsonProperty("forceIPv6")] public bool ForceIPv6 { get; set; }

        [JsonProperty("maxPeers")] public ushort MaxPeers { get; set; }

        [JsonProperty("hubAddress")] public string HubAddress { get; set; } = "127.0.0.1:50001";
    }
}