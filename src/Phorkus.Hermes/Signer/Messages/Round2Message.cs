using System.Collections.Generic;
using Org.BouncyCastle.Math;

namespace Phorkus.Hermes.Signer.Messages
{
    public class Round2Message
    {
        public IEnumerable<BigInteger> openUiVi;
        public Zkpi1 zkp1;
        
        public Round2Message(IEnumerable<BigInteger> openUiVi, Zkpi1 zkp1) {
            this.openUiVi = openUiVi;
            this.zkp1 = zkp1;
        }
    }
}