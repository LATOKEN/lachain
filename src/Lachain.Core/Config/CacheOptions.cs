using Newtonsoft.Json;

namespace Lachain.Core.Config
{
    public class CacheOptions
    {
        [JsonProperty("peers")] public int? SizeLimit { get; set; }
    }
}
