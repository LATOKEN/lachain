using System;
using Org.BouncyCastle.Math;

namespace Phorkus.Party.Math
{
    public class PolynomialMod : Polynomial
    {
        protected readonly BigInteger Mod;

        /** Construct a random polynomial over the integers mod N given its degree and intercept
         * @param degree the degree of this polynomial
         * @param mod N
         * @param a0 the intercept <code>f(0)</code>
         * @param numbit the bitlength of the coefficients
         * @param random a randomness generator
         */

        public PolynomialMod(int degree, BigInteger mod, BigInteger a0, int numbit, Random random)
            : base(degree, a0, numbit, random)
        {
            Mod = mod;
            A[0] = A[0].Mod(mod);
            for (var t = 0; t <= degree; t++)
            {
                A[t] = A[t].Mod(mod);
            }
        }

        public BigInteger eval(int x)
        {
            var result = A[0];
            BigInteger powx;
            BigInteger term;
            /* TODO: "this code can be optimized" */
            for (var i = 1; i < A.Length; i++)
            {
                powx = BigInteger.ValueOf((long) System.Math.Pow(x, i));
                term = A[i].Multiply(powx).Mod(Mod);
                result = result.Add(term).Mod(Mod);
            }

            return result;
        }
    }
}