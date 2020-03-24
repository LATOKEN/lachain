using Newtonsoft.Json;

namespace Lachain.Storage
{
    public class StorageConfig
    {
        [JsonProperty("provider")]
        public string? Provider { get; set; }
        
        [JsonProperty("path")]
        public string? Path { get; set; }
    }
}