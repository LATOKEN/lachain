using System.Collections.Generic;
using Newtonsoft.Json;

namespace Lachain.Core.Consensus
{
    public class ConsensusConfig
    {
        [JsonProperty("ECDSAPrivateKey")] public string? EcdsaPrivateKey { get; set; }
        [JsonProperty("TPKEPrivateKey")] public string? TpkePrivateKey { get; set; }

        [JsonProperty("ThresholdSignaturePrivateKey")]
        public string? ThresholdSignaturePrivateKey { get; set; }
    }
}