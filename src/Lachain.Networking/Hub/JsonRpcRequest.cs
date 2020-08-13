using Newtonsoft.Json;

namespace Lachain.Networking.Hub
{
    [JsonObject]
    public class JsonRpcRequest<T>
    {
        [JsonProperty("jsonrpc")] public string JsonRpc = "2.0";
        [JsonProperty("id")] public string Id = "0";
        [JsonProperty("method")] public string Method;
        [JsonProperty("params")] public T Params;
    }
}