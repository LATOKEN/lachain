using Org.BouncyCastle.Math;

namespace Phorkus.Hermes.Generator
{
    public class GenesisPrivateParams
    {
		/** Large prime P' used for secret sharing with polynomials over the integer mod P' */
		public BigInteger P;
		
		/**The number of parties in the key generation protocol*/
		public int n;
		
		/** The maximum number of parties an adversary can corrupt without breaking the security of the protocol*/
		public int t;
		
		/** The bitlength of p and q, so that N has a security of 2k */
		public int k;
		
		/** The security of the statistical hiding of &Phi;(N) with &Beta; and R */
		public int K;
	    
		internal GenesisPrivateParams(BigInteger Pp, int t, int k, int K, int n) {
			this.P = Pp;
			this.t = t;
			this.k = k;
			this.K = K;
			this.n = n;
		}
    }
}