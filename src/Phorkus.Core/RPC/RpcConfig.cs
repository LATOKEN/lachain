using Newtonsoft.Json;

namespace Phorkus.Core.RPC
{
    [JsonObject]
    public class RpcConfig
    {
        public static readonly RpcConfig Default = new RpcConfig
        {
            Host = "localhost",
            Port = 7070
        };
        
        [JsonProperty("host")]
        public string Host { get; set; }

        [JsonProperty("port")]
        public short Port { get; set; }
    }
}