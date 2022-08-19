using System.Collections.Generic;
using Lachain.CommunicationHub.Net;
using Newtonsoft.Json;

namespace Lachain.Core.Vault
{
    [JsonObject]
    public class JsonWallet
    {
        public JsonWallet(
            ulong version, 
            string ecdsaPrivateKey,
            string hubPrivateKey,
            Dictionary<ulong, string> tpkePrivateKeys,
            Dictionary<ulong, List<string>> tpkeVerificationKeys,
            Dictionary<ulong, string> thresholdSignatureKeys
        )
        {
            Version = version;
            TpkePrivateKeys = tpkePrivateKeys;
            TpkeVerificationKeys = tpkeVerificationKeys;
            HubPrivateKey = hubPrivateKey;
            ThresholdSignatureKeys = thresholdSignatureKeys;
            EcdsaPrivateKey = ecdsaPrivateKey;
        }

        [JsonProperty("version")] public ulong? Version { get; set; }
        
        [JsonProperty("tpkeKeys")] public Dictionary<ulong, string>? TpkePrivateKeys { get; set; }

        [JsonProperty("tpkeVerificationKeys")] public Dictionary<ulong, List<string>>? TpkeVerificationKeys { get; set; }

        [JsonProperty("thresholdSignatureKeys")]
        public Dictionary<ulong, string>? ThresholdSignatureKeys { get; set; }

        [JsonProperty("ecdsaPrivateKey")] public string? EcdsaPrivateKey { get; set; }

        [JsonProperty("hubPrivateKey")] public string? HubPrivateKey { get; set; }
    }
}