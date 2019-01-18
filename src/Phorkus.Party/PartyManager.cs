using System.Collections.Generic;
using Phorkus.Party.Signer;

namespace Phorkus.Party
{
    public class PartyManager : IPartyManager
    {
        public IGeneratorProtocol CreateGeneratorProtocol(uint parties, uint threshold, ulong keyLength)
        {
            throw new System.NotImplementedException();
        }
        
        public ISignerProtocol CreateSignerProtocol(byte[] share, IEnumerable<byte> privateKey, byte[] publicKey, string curveType)
        {
            return new DefaultSignerProtocol(share, privateKey, publicKey, curveType);
        }
    }
}