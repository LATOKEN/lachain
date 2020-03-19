using Phorkus.Crypto;
using Phorkus.Crypto.ThresholdSignature;
using Phorkus.Crypto.TPKE;

namespace Phorkus.Consensus
{
    public class PrivateConsensusKeySet : IPrivateConsensusKeySet
    {
        public PrivateConsensusKeySet(
            ECDSAKeyPair ecdsaKeyPair, PrivateKey tpkePrivateKey,
            PrivateKeyShare thresholdSignaturePrivateKeyShare)
        {
            EcdsaKeyPair = ecdsaKeyPair;
            TpkePrivateKey = tpkePrivateKey;
            ThresholdSignaturePrivateKeyShare = thresholdSignaturePrivateKeyShare;
        }

        public PrivateKey TpkePrivateKey { get; }
        public PrivateKeyShare ThresholdSignaturePrivateKeyShare { get; }
        public ECDSAKeyPair EcdsaKeyPair { get; }
    }
}