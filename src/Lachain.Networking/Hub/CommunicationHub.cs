using System;
using System.Linq;
using Lachain.Logger;
using Lachain.Proto;
using Lachain.Utility.Utils;
using Newtonsoft.Json;
using RestSharp;

namespace Lachain.Networking.Hub
{
    public class CommunicationHub
    {
        private static readonly ILogger<CommunicationHub> Logger = LoggerFactory.GetLoggerForClass<CommunicationHub>();

        [JsonObject]
        public class SendRequest
        {
            [JsonProperty("to")] public string To;
            [JsonProperty("publicKey")] public string PublicKey;
            [JsonProperty("signature")] public string Signature;
            [JsonProperty("payload")] public string Payload;
        }

        [JsonObject]
        public class ReceiveRequest
        {
            [JsonProperty("timestamp")] public ulong Timestamp;
            [JsonProperty("publicKey")] public string PublicKey;
            [JsonProperty("signature")] public string Signature;
        }

        [JsonObject]
        public class ReceiveResponse
        {
            [JsonProperty("payload")] public string Payload;
            [JsonProperty("from")] public string From;
        }

        private static readonly IRestClient Client = new RestClient("http://95.217.215.141:9090")
            .UseSerializer<JsonSerializer>();

        public static void Send(ECDSAPublicKey from, ECDSAPublicKey to, byte[] payload, Signature signature)
        {
            var request = new RestRequest();
            request.AddHeader("Content-Type", "application/json");
            request.AddHeader("Accept", "application/json");
            request.AddJsonBody(new JsonRpcRequest<SendRequest>
            {
                Method = "send",
                Params = new SendRequest
                {
                    Payload = payload.ToHex(false),
                    PublicKey = from.ToHex(false),
                    Signature = signature.ToHex(false),
                    To = to.ToHex(false)
                }
            });
            var response = Client.Post(request);
            if (!response.IsSuccessful)
                Logger.LogError($"Cannot send message to communication hub: {response.ErrorMessage}");
            var parsed = JsonConvert.DeserializeObject<JsonRpcResponse<string?>?>(response.Content);
            if (parsed?.Error != null)
                Logger.LogError($"Cannot send data to communication hub: {parsed.Error.message}");
        }

        public static byte[][] Receive(ECDSAPublicKey publicKey, ulong timestamp, Signature signature)
        {
            var request = new RestRequest();
            request.AddHeader("Content-Type", "application/json");
            request.AddHeader("Accept", "application/json");
            request.AddJsonBody(new JsonRpcRequest<ReceiveRequest>
            {
                Method = "receive",
                Params = new ReceiveRequest
                {
                    PublicKey = publicKey.ToHex(false),
                    Signature = signature.ToHex(false),
                    Timestamp = timestamp
                }
            });
            var response = Client.Post(request);
            var parsed = JsonConvert.DeserializeObject<JsonRpcResponse<ReceiveResponse[]>>(response.Content);
            if (parsed.Error != null)
                throw new Exception($"Cannot get data from hub: {parsed.Error.message}");
            return parsed.Result
                .Select(x => x.Payload.HexToBytes())
                .ToArray();
        }
    }
}