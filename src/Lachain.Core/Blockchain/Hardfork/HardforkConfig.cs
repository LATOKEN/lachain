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
        [JsonProperty("hardfork_6")] public ulong? Hardfork_6 { get; set; }
        [JsonProperty("hardfork_7")] public ulong? Hardfork_7 { get; set; }
        [JsonProperty("hardfork_8")] public ulong? Hardfork_8 { get; set; }
        [JsonProperty("hardfork_9")] public ulong? Hardfork_9 { get; set; }
    }
}