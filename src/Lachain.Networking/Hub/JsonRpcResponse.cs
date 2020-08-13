using Newtonsoft.Json;

namespace Lachain.Networking.Hub
{
    [JsonObject]
    public class JsonRpcRespnse<T>
    {
        [JsonProperty("jsonrpc")] public string JsonRpc = "2.0";
        [JsonProperty("id")] public string Id = "0";
        [JsonProperty("result")] public T Result;
    }
}