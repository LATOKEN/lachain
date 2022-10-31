using Lachain.Crypto.ECDSA;

namespace Lachain.Consensus
{
    public interface IPrivateConsensusKeySet
    {
        Crypto.ThresholdSignature.PrivateKeyShare ThresholdSignaturePrivateKeyShare { get; }
        EcdsaKeyPair EcdsaKeyPair { get; }
    }
}