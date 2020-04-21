using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace Lachain.Core.Blockchain.Genesis
{
    public class GenesisConfig
    {
        public class ValidatorInfo
        {
            [JsonProperty("ECDSAPublicKey")] public string? EcdsaPublicKey;

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
        
        public void ValidateOrThrow()
        {
            if (Validators.Count == 0) throw new ArgumentException("Initial validators must be specified in genesis config");
            if (Validators.Any(v => v.EcdsaPublicKey is null || v.ResolvableName is null || v.ThresholdSignaturePublicKey is null))
                throw new ArgumentException("Incorrect validator information in config");
            if (ThresholdEncryptionPublicKey is null)
                throw new ArgumentException("Initial threshold encryption keyring is incomplete");
        }
    }
}