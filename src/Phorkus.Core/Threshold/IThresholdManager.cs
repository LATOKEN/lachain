using Phorkus.Crypto;
using Phorkus.Network.Grpc;
using Phorkus.Proto;

namespace Phorkus.Core.Threshold
{
    public interface IThresholdManager
    {
        ThresholdKey GeneratePrivateKey();
        
        Signature SignData(KeyPair keyPair, string curveType, byte[] message);

        ThresholdMessage HandleThresholdMessage(ThresholdMessage thresholdMessage, PublicKey publicKey);
    }
}