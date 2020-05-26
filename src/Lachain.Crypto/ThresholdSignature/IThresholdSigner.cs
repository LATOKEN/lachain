namespace Lachain.Crypto.ThresholdSignature
{
    public interface IThresholdSigner
    {
        Signature Sign();
        bool AddShare(int idx, Signature sigShare, out Signature? signature);
    }
}