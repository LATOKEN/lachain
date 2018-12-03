using System.Collections.Generic;
using Phorkus.Crypto;
using Phorkus.Hermes;
using Phorkus.Proto;

namespace Phorkus.Core.Hermes
{
    public class HermesSigner : IHermesSigner
    {
        private readonly ICrypto _crypto;

        private readonly IPartyManager _partyManager
            = new PartyManager();
        
        public HermesSigner(ICrypto crypto)
        {
            _crypto = crypto;
        }

        public ThresholdKey GeneratePrivateKey(IEnumerable<PublicKey> validators)
        {
            throw new System.NotImplementedException();
        }
        
        public Signature SignData(IEnumerable<PublicKey> validators, string curveType, byte[] data)
        {
            throw new System.NotImplementedException();
        }
    }
}