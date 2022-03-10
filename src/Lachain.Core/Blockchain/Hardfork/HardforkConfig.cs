using Newtonsoft.Json;

namespace Lachain.Core.Blockchain.Hardfork
{
    public class HardforkConfig
    {
        [JsonProperty("hardfork_1")] public ulong? Hardfork_1 { get; set; }
        [JsonProperty("hardfork_2")] public ulong? Hardfork_2 { get; set; }
        [JsonProperty("hardfork_3")] public ulong? Hardfork_3 { get; set; }
        [JsonProperty("hardfork_4")] public ulong? Hardfork_4 { get; set; }
        [JsonProperty("hardfork_5")] public ulong? Hardfork_5 { get; set; }
    }
}