using System;
using Org.BouncyCastle.Math;
using Phorkus.Hermes.Generator.State;
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
        /** Generates the private parameters for a given party in the BGW protocol
 * @param i the id of the party. i &in; [1,n], n the number of parties
 * @param protParam the security parameters
 * @param rand a randomness generator
 * @return the generated parameters
 */
        public static BgwPrivateParams genFor(int i, ProtocolParameters protParam, Random rand) {
			
            if (i < 1 || i > protParam.n)
                throw new ArgumentException("i must be between 1 and the number of parties");
			
            // p and q generation
            BigInteger p;
            BigInteger q;
            BigInteger min = BigInteger.One.ShiftLeft(protParam.k-1);
            BigInteger max = BigInteger.One.ShiftLeft(protParam.k).Subtract(BigInteger.One);
            BigInteger four = BigInteger.ValueOf(4);
            BigInteger modFourTarget = i == 1 ? BigInteger.ValueOf(3) : BigInteger.Zero;
			
            do {
                p = min.Add(new BigInteger(protParam.k-1, rand));
                q = min.Add(new BigInteger(protParam.k-1, rand));
            } while(p.CompareTo(max) > 0 ||
                    q.CompareTo(max) > 0 ||
                    ! p.Mod(four).Equals(modFourTarget) ||
                    ! q.Mod(four).Equals(modFourTarget));
			
            // polynomials generation
			
            PolynomialMod f = new PolynomialMod(protParam.t, protParam.P, p, protParam.k, rand);
            PolynomialMod g = new PolynomialMod(protParam.t, protParam.P, q, protParam.k, rand);
            PolynomialMod h = new PolynomialMod(2*protParam.t, protParam.P, BigInteger.Zero, protParam.k, rand); 
			
            return new BgwPrivateParams(i, protParam.n, p, q, f, g, h);
        }
    }
}