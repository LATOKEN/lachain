namespace Phorkus.Hermes
{
    public interface IPartyManager
    {
        IGeneratorProtocol CreateGeneratorProtocol(uint parties, uint threshold, ulong keyLength);

        ISignerProtocol CreateSignerProtocol();
    }
}