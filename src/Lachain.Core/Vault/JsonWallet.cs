using System.Collections.Generic;
using Newtonsoft.Json;

namespace Lachain.Core.Vault
{
    [JsonObject]
    public class JsonWallet
    {
        public JsonWallet(
            string ecdsaPrivateKey,
            Dictionary<ulong, string> tpkePrivateKeys,
            Dictionary<ulong, string> thresholdSignatureKeys
        )
        {
            TpkePrivateKeys = tpkePrivateKeys;
            ThresholdSignatureKeys = thresholdSignatureKeys;
            EcdsaPrivateKey = ecdsaPrivateKey;
        }

        [JsonProperty("tpkeKeys")] public Dictionary<ulong, string>? TpkePrivateKeys { get; set; }

        [JsonProperty("thresholdSignatureKeys")]
        public Dictionary<ulong, string>? ThresholdSignatureKeys { get; set; }

        [JsonProperty("ecdsaPrivateKey")] public string? EcdsaPrivateKey { get; set; }
    }
}