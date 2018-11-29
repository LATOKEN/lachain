namespace Phorkus.Hermes
{
    public interface IPartyManager
    {
        IGeneratorProtocol CreateParty(uint parties, uint threshold, ulong keyLength);
    }
}