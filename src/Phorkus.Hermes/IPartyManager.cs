namespace Phorkus.Hermes
{
    public interface IPartyManager
    {
        IPartyProtocol CreateParty(uint parties, uint threshold, ulong keyLength);
    }
}