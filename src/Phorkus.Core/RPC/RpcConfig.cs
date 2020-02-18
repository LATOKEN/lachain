using Newtonsoft.Json;

namespace Phorkus.Core.RPC
{
    [JsonObject]
    public class RpcConfig
    {
        public static readonly RpcConfig Default = new RpcConfig
        {
            Hosts = new[]
            {
                "localhost"
            },
            Port = 7070
        };
        
        [JsonProperty("hosts")]
        public string[]? Hosts { get; set; }

        [JsonProperty("port")]
        public short Port { get; set; }
    }
}