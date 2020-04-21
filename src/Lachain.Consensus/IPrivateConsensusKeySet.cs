using Lachain.Crypto.ECDSA;

namespace Lachain.Consensus
{
    public interface IPrivateConsensusKeySet
    {
        Crypto.TPKE.PrivateKey TpkePrivateKey { get; }
        Crypto.ThresholdSignature.PrivateKeyShare ThresholdSignaturePrivateKeyShare { get; }
        EcdsaKeyPair EcdsaKeyPair { get; }
    }
}