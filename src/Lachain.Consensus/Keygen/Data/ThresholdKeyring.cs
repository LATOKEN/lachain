namespace Lachain.Consensus.Keygen.Data
{
    public class ThresholdKeyring
    {
        public Crypto.TPKE.PrivateKey TpkePrivateKey;
        public Crypto.TPKE.PublicKey TpkePublicKey;
        public Crypto.ThresholdSignature.PublicKeySet ThresholdSignaturePublicKeySet;
        public Crypto.ThresholdSignature.PrivateKeyShare ThresholdSignaturePrivateKey;
    }
}