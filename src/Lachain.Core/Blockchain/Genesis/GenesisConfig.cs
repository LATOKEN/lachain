using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace Lachain.Core.Blockchain.Genesis
{
    public class GenesisConfig
    {
        public GenesisConfig(string thresholdEncryptionPublicKey, string blockReward, string basicGasPrice)
        {
            ThresholdEncryptionPublicKey = thresholdEncryptionPublicKey;
            BlockReward = blockReward;
            BasicGasPrice = basicGasPrice;
        }

        [JsonProperty("balances")]
        public Dictionary<string, string> Balances { get; set; } = new Dictionary<string, string>();

        [JsonProperty("validators")] public List<ValidatorInfo> Validators { get; set; } = new List<ValidatorInfo>();

        [JsonProperty("TPKEPublicKey")] public string ThresholdEncryptionPublicKey;

        [JsonProperty("blockReward")] public string BlockReward;

        [JsonProperty("basicGasPrice")] public string BasicGasPrice;

        public void ValidateOrThrow()
        {
            if (Validators.Count == 0)
                throw new ArgumentException("Initial validators must be specified in genesis config");
            if (Validators.Any(v =>
                v.EcdsaPublicKey is null || v.ResolvableName is null || v.ThresholdSignaturePublicKey is null))
                throw new ArgumentException("Incorrect validator information in config");
            if (ThresholdEncryptionPublicKey is null)
                throw new ArgumentException("Initial threshold encryption keyring is incomplete");
            if (BlockReward is null)
                throw new ArgumentException("Initial block reward must be specified in config");
            if (BasicGasPrice is null)
                throw new ArgumentException("Initial basic gas price must be specified in config");
        }
    }
}