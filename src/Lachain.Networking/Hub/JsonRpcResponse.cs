using Newtonsoft.Json;

namespace Lachain.Networking.Hub
{
    public class JsonRpcError
    {
        [JsonProperty("message")] public string message;
        [JsonProperty("code")] public int code;
    }
    
    [JsonObject]
    public class JsonRpcResponse<T>
    {
        [JsonProperty("jsonrpc")] public string JsonRpc = "2.0";
        [JsonProperty("id")] public string Id = "0";
        [JsonProperty("result")] public T Result;
        [JsonProperty("error")] public JsonRpcError? Error;
    }
}