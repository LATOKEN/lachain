using Newtonsoft.Json;

namespace Lachain.Core.Blockchain.Genesis
{
    public class ValidatorInfo
    {
        [JsonProperty("ECDSAPublicKey")] public string EcdsaPublicKey;

        [JsonProperty("thresholdSignaturePublicKey")]
        public string ThresholdSignaturePublicKey;

        [JsonProperty("stakerAddress")]
        public string StakerAddress;

        [JsonProperty("stakeAmount")]
        public string StakeAmount;


        public ValidatorInfo(string ecdsaPublicKey, string thresholdSignaturePublicKey)
        {
            EcdsaPublicKey = ecdsaPublicKey;
            ThresholdSignaturePublicKey = thresholdSignaturePublicKey;
        }
    }
}