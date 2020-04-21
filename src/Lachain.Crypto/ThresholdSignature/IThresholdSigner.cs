namespace Lachain.Crypto.ThresholdSignature
{
    public interface IThresholdSigner
    {
        SignatureShare Sign();
        bool AddShare(PublicKey pubKey, SignatureShare sigShare, out Signature? signature);
    }
}