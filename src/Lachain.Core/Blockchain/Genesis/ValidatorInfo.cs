using Newtonsoft.Json;

namespace Lachain.Core.Blockchain.Genesis
{
    public class ValidatorInfo
    {
        [JsonProperty("ECDSAPublicKey")] public string EcdsaPublicKey;

        [JsonProperty("thresholdSignaturePublicKey")]
        public string ThresholdSignaturePublicKey;
        
        public ValidatorInfo(string ecdsaPublicKey, string thresholdSignaturePublicKey)
        {
            EcdsaPublicKey = ecdsaPublicKey;
            ThresholdSignaturePublicKey = thresholdSignaturePublicKey;
        }
    }
}