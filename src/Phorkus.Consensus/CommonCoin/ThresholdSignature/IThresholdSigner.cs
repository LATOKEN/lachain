namespace Phorkus.Consensus.CommonCoin.ThresholdSignature
{
    public interface IThresholdSigner
    {
        SignatureShare Sign();
        bool AddShare(PublicKeyShare pubKey, SignatureShare sigShare, out Signature signature);
    }
}