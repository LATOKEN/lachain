namespace Phorkus.Hermes
{
    public class PartyManager : IPartyManager
    {
        public IGeneratorProtocol CreateGeneratorProtocol(uint parties, uint threshold, ulong keyLength)
        {
            throw new System.NotImplementedException();
        }
        
        public ISignerProtocol CreateSignerProtocol()
        {
            throw new System.NotImplementedException();
        }
    }
}