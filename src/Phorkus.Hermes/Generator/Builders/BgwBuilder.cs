using System;
using Org.BouncyCastle.Math;
using Phorkus.Hermes.Generator.State;
using Phorkus.Hermes.Math;

namespace Phorkus.Hermes.Generator.Builders
{
    public static class BgwBuilder
    {
        /** Generates the private parameters for a given party in the BGW protocol
         * @param i the id of the party. i &in; [1,n], n the number of parties
         * @param protParam the security parameters
         * @param rand a randomness generator
         * @return the generated parameters
         */
        public static BgwPrivateParams GeneratePrivateFor(int i, GenesisPrivateParams protPrivateParam, Random rand)
        {
            if (i < 1 || i > protPrivateParam.n)
                throw new ArgumentException("i must be between 1 and the number of parties");

            // p and q generation
            BigInteger p;
            BigInteger q;
            var min = BigInteger.One.ShiftLeft(protPrivateParam.k - 1);
            var max = BigInteger.One.ShiftLeft(protPrivateParam.k).Subtract(BigInteger.One);
            var four = BigInteger.ValueOf(4);
            var modFourTarget = i == 1 ? BigInteger.ValueOf(3) : BigInteger.Zero;

            do
            {
                p = min.Add(new BigInteger(protPrivateParam.k - 1, rand));
                q = min.Add(new BigInteger(protPrivateParam.k - 1, rand));
            } while (p.CompareTo(max) > 0 ||
                     q.CompareTo(max) > 0 ||
                     !p.Mod(four).Equals(modFourTarget) ||
                     !q.Mod(four).Equals(modFourTarget));

            // polynomials generation
            var f = new PolynomialMod(protPrivateParam.t, protPrivateParam.P, p, protPrivateParam.k, rand);
            var g = new PolynomialMod(protPrivateParam.t, protPrivateParam.P, q, protPrivateParam.k, rand);
            var h = new PolynomialMod(2 * protPrivateParam.t, protPrivateParam.P, BigInteger.Zero, protPrivateParam.k, rand);

            return new BgwPrivateParams(i, protPrivateParam.n, p, q, f, g, h);
        }

        public static BgwPublicParams GeneratePublicFor(int j, BgwPrivateParams bgwPrivParam)
        {
            if (j < 1 || j > bgwPrivParam.n)
                throw new ArgumentException("j must be between 1 and the number of parties");

            var i = bgwPrivParam.i;
            var pj = bgwPrivParam.fi.eval(j);
            var qj = bgwPrivParam.gi.eval(j);
            var hj = bgwPrivParam.hi.eval(j);

            return new BgwPublicParams(i, j, bgwPrivParam.n, pj, qj, hj);
        }
 
    }
}