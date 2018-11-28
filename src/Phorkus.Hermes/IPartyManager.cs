namespace Phorkus.Hermes
{
    public interface IPartyManager
    {
        IPartyBuilder CreateParty(uint parties, uint threshold, ulong keyLength);
    }
}