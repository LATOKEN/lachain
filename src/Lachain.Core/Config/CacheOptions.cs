using Newtonsoft.Json;

namespace Lachain.Core.Config
{
    public class CacheOptions
    {
        [JsonProperty("sizeLimit")] public int? SizeLimit { get; set; }
    }
}
