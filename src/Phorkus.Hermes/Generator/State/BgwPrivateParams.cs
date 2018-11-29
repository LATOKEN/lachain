using Org.BouncyCastle.Math;
using Phorkus.Hermes.Math;

namespace Phorkus.Hermes.Generator
{
    public class BgwPrivateParams
    {
        /** The id of the party. i &in; [1;N]*/
        public int i;

        /** the number of parties*/
        public int n;

        /** The contribution of party Pi to p = &sum;<sup>N</sup><sub>i=1</sub>   pi*/
        public BigInteger pi;

        /** The contribution of party Pi to q = &sum;<sup>N</sup><sub>i=1</sub>   qi*/
        public BigInteger qi;

        /** The polynomial used to share pi*/
        public PolynomialMod fi;

        /** The polynomial used to share qi*/
        public PolynomialMod gi;

        /** The polynomial used to share Ni*/
        public PolynomialMod hi;
        
        internal BgwPrivateParams(int i, int n, BigInteger p, BigInteger q, PolynomialMod f, PolynomialMod g,
            PolynomialMod h)
        {
            this.i = i;
            this.n = n;
            pi = p;
            qi = q;
            fi = f;
            gi = g;
            hi = h;
        }
    }
}