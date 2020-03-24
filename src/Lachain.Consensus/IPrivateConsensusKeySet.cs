using Lachain.Crypto;
using Lachain.Proto;

namespace Lachain.Consensus
{
    public interface IPrivateConsensusKeySet
    {
        Crypto.TPKE.PrivateKey TpkePrivateKey { get; }
        Crypto.ThresholdSignature.PrivateKeyShare ThresholdSignaturePrivateKeyShare { get; }
        ECDSAKeyPair EcdsaKeyPair { get; }
    }
}