namespace Lachain.Consensus.ThresholdKeygen.Data
{
    public struct ThresholdKeyring
    {
        public Crypto.TPKE.PrivateKey TpkePrivateKey;
        public Crypto.TPKE.PublicKey TpkePublicKey;
        public Crypto.ThresholdSignature.PublicKeySet ThresholdSignaturePublicKeySet;
        public Crypto.ThresholdSignature.PrivateKeyShare ThresholdSignaturePrivateKey;
    }
}