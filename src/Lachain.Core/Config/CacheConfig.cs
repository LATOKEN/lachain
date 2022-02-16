using Newtonsoft.Json;

namespace Lachain.Core.Config
{
    public class CacheConfig
    {
        [JsonProperty("blockHeight")] public CacheOptions BlockHeight { get; set; }
    }
}
