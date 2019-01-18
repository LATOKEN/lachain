using System;
using Org.BouncyCastle.Math;

namespace Phorkus.Party.Math
{
    public class Polynomial
    {
        protected readonly BigInteger[] A;

        /** Constructs a new random polynomial with the given degree and intercept
         * @param degree the degree of the polynomial
         * @param a0 the intercept of the polynomial <code>f(0)</code>
         * @param numbit the bitlength of the coefficients
         * @param random a randomness generator
         */
        public Polynomial(int degree, BigInteger a0, int numbit, Random random)
        {
            A = new BigInteger[degree + 1];
            A[0] = a0;
            for (var t = 1; t <= degree; t++)
            {
                A[t] = new BigInteger(numbit, random);
            }
        }

        /** Evaluate this polynomial in <code>x</code>
         * @param x <code>x</code>
         * @return <code>f(x)</code>
         */
        public BigInteger eval(int x)
        {
            var result = A[0];
            BigInteger powx;
            BigInteger term;
            /* TODO: "this code can be optimized" */
            for (var i = 1; i < A.Length; i++)
            {
                powx = BigInteger.ValueOf((long) System.Math.Pow(x, i));
                term = A[i].Multiply(powx);
                result = result.Add(term);
            }

            return result;
        }
    }
}