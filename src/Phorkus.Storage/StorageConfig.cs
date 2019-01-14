using Newtonsoft.Json;

namespace Phorkus.Storage
{
    public class StorageConfig
    {
        [JsonProperty("provider")]
        public string Provider { get; set; }
        
        [JsonProperty("path")]
        public string Path { get; set; }
    }
}