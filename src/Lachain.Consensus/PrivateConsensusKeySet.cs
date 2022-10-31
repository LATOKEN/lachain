using Lachain.Crypto.ECDSA;
using Lachain.Crypto.ThresholdSignature;
using Lachain.Crypto.TPKE;

namespace Lachain.Consensus
{
    public class PrivateConsensusKeySet : IPrivateConsensusKeySet
    {
        public PrivateConsensusKeySet(
            EcdsaKeyPair ecdsaKeyPair, PrivateKey tpkePrivateKey,
            PrivateKeyShare thresholdSignaturePrivateKeyShare)
        {
            EcdsaKeyPair = ecdsaKeyPair;
            ThresholdSignaturePrivateKeyShare = thresholdSignaturePrivateKeyShare;
        }

        public PrivateKeyShare ThresholdSignaturePrivateKeyShare { get; }
        public EcdsaKeyPair EcdsaKeyPair { get; }
    }
}