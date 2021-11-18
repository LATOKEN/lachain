using Newtonsoft.Json;

namespace Lachain.Core.Blockchain.Hardfork
{
    public class HardforkConfig
    {
        const string hardfork_1 = "hardfork_1";
        
        [JsonProperty(hardfork_1)] public ulong? Hardfork_1 { get; set; }
    }
}