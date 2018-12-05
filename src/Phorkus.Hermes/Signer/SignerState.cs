namespace Phorkus.Hermes.Signer
{
    public enum SignerState : byte
    {
        Initialization,
        Round1,
        Round2,
        Round3,
        Round4,
        Round5,
        Round6,
        Finalization
    }
}