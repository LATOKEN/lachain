using System.Collections.Generic;
using Lachain.CommunicationHub.Net;
using Newtonsoft.Json;

namespace Lachain.Core.Vault
{
    [JsonObject]
    public class JsonWallet
    {
        public JsonWallet(
            string ecdsaPrivateKey,
            string hubPrivateKey,
            Dictionary<ulong, string> tpkePrivateKeys,
            Dictionary<ulong, string> thresholdSignatureKeys
        )
        {
            TpkePrivateKeys = tpkePrivateKeys;
            HubPrivateKey = hubPrivateKey;
            ThresholdSignatureKeys = thresholdSignatureKeys;
            EcdsaPrivateKey = ecdsaPrivateKey;
        }

        [JsonProperty("tpkeKeys")] public Dictionary<ulong, string>? TpkePrivateKeys { get; set; }

        [JsonProperty("thresholdSignatureKeys")]
        public Dictionary<ulong, string>? ThresholdSignatureKeys { get; set; }

        [JsonProperty("ecdsaPrivateKey")] public string? EcdsaPrivateKey { get; set; }

        [JsonProperty("hubPrivateKey")] public string? HubPrivateKey { get; set; }
    }
}