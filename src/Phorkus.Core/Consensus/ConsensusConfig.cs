using System.Collections.Generic;
using Newtonsoft.Json;

namespace Phorkus.Core.Consensus
{
    public class ConsensusConfig
    {
        [JsonProperty("validators")] public List<string> ValidatorsEcdsaPublicKeys { get; set; }

        [JsonProperty("ECDSAPrivateKey")] public string EcdsaPrivateKey { get; set; }

        [JsonProperty("TPKEPrivateKey")] public string TpkePrivateKey { get; set; }

        [JsonProperty("TPKEPublicKey")] public string TpkePublicKey { get; set; }

        [JsonProperty("TPKEVerificationKey")] public string TpkeVerificationKey { get; set; }

        [JsonProperty("ThresholdSignaturePublicKeys")]
        public List<string> ThresholdSignaturePublicKeySet { get; set; }

        [JsonProperty("ThresholdSignaturePrivateKey")]
        public string ThresholdSignaturePrivateKey { get; set; }
    }
}