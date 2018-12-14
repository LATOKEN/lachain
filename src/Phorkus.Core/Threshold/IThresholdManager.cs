using Phorkus.Crypto;
using Phorkus.Proto;

namespace Phorkus.Core.Threshold
{
    public interface IThresholdManager
    {
        ThresholdKey GeneratePrivateKey();
        
        byte[] SignData(KeyPair keyPair, string curveType, byte[] message);

        ThresholdRequest HandleThresholdMessage(ThresholdRequest thresholdMessage, PublicKey publicKey);
    }
}