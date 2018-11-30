using Org.BouncyCastle.Math;

namespace Phorkus.Hermes.Signer
{
    public class DSASignature
    {
        public BigInteger r;
        public BigInteger s;
	
        public DSASignature(BigInteger r, BigInteger s) {
            this.r = r;
            this.s = s;
        }
    }
}