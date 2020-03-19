using Phorkus.Crypto;
using Phorkus.Proto;

namespace Phorkus.Consensus
{
    public interface IPrivateConsensusKeySet
    {
        Crypto.TPKE.PrivateKey TpkePrivateKey { get; }
        Crypto.ThresholdSignature.PrivateKeyShare ThresholdSignaturePrivateKeyShare { get; }
        ECDSAKeyPair EcdsaKeyPair { get; }
    }
}