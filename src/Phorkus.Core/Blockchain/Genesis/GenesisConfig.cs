using System.Collections.Generic;
using Newtonsoft.Json;

namespace Phorkus.Core.Blockchain.Genesis
{
    public class GenesisConfig
    {
        public class ValidatorInfo
        {
            [JsonProperty("ecdsaPublicKey")] public string? EcdsaPublicKey;

            [JsonProperty("thresholdSignaturePublicKey")]
            public string? ThresholdSignaturePublicKey;
            
            [JsonProperty("resolvableName")]
            public string? ResolvableName;
        }

        [JsonProperty("balances")]
        public Dictionary<string, string> Balances { get; set; } = new Dictionary<string, string>();
        
        [JsonProperty("validators")]
        public List<ValidatorInfo> Validators { get; set; } = new List<ValidatorInfo>();

        [JsonProperty("TPKEPublicKey")]
        public string? ThresholdEncryptionPublicKey;
        
        [JsonProperty("TPKEVerificationKey")]
        public string? ThresholdEncryptionVerificationKey;
    }
}