namespace Lachain.Crypto.ThresholdSignature
{
    public interface IThresholdSigner
    {
        SignatureShare Sign();
        bool AddShare(int idx, SignatureShare sigShare, out Signature? signature);
    }
}