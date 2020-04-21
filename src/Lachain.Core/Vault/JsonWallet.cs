using System.Collections.Generic;
using Newtonsoft.Json;

namespace Lachain.Core.Vault
{
    [JsonObject]
    public class JsonWallet
    {
        [JsonProperty("tpkeKeys")] public Dictionary<ulong, string> TpkePrivateKeys { get; set; }
        
        [JsonProperty("thresholdSignatureKeys")] public Dictionary<ulong, string> ThresholdSignatureKeys { get; set; }
        
        [JsonProperty("ecdsaPrivateKey")] public string EcdsaPrivateKey { get; set; }
    }
}