using Newtonsoft.Json;

namespace Lachain.Core.Blockchain.Genesis
{
    public class ValidatorInfo
    {
        [JsonProperty("ECDSAPublicKey")] public string EcdsaPublicKey;

        [JsonProperty("thresholdSignaturePublicKey")]
        public string ThresholdSignaturePublicKey;

        [JsonProperty("resolvableName")] public string ResolvableName;

        public ValidatorInfo(string ecdsaPublicKey, string thresholdSignaturePublicKey, string resolvableName)
        {
            EcdsaPublicKey = ecdsaPublicKey;
            ThresholdSignaturePublicKey = thresholdSignaturePublicKey;
            ResolvableName = resolvableName;
        }
    }
}