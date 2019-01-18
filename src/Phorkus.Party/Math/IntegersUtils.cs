using System;
using System.Collections.Generic;
using Org.BouncyCastle.Math;

namespace Phorkus.Party.Math
{
    /**
     * Provides some of the required operations on the integers like random picking in range, factorial,...
     * @author Christian Mouchet
     * */
    public static class IntegersUtils
    {
        /** Picks an integer at random in the given range, boundaries included.
         * @param min the lower bound of the range
         * @param max the upper bound of the range
         * @param rand a randomness generator
         * @return a random integer in the range [min; max]
         */
        public static BigInteger PickInRange(BigInteger min, BigInteger max, Random rand)
        {
            BigInteger candidate;
            do
            {
                candidate = min.Add(new BigInteger(max.BitLength, rand));
            } while (candidate.CompareTo(max) > 0);

            return candidate;
        }

        public static BigInteger PickPrimeInRange(BigInteger min, BigInteger max, Random rand)
        {
            BigInteger candidate;
            do
            {
                candidate = min.Add(BigInteger.ProbablePrime(max.BitLength - 1, rand));
            } while (candidate.CompareTo(max) > 0);

            return candidate;
        }

        /** Picks a random integer that is a generator of Z<sub>N<sup>2</sup></sub> with high probability.
         * @param N is N
         * @param bitLength the size of the integer to be picked in bit
         * @param rand a randomness generator
         * @return a probable generator of of Z<sub>N<sup>2</sup></sub>
         */
        public static BigInteger PickProbableGeneratorOfZnSquare(BigInteger N, int bitLength, Random rand)
        {
            BigInteger r;
            do
            {
                r = new BigInteger(bitLength, rand);
            } while (!BigInteger.One.Equals(r.Gcd(N)));

            return r.Multiply(r).Mod(N.Multiply(N));
        }

        /** Computes the factorial of an integer.
         * @param n an integer
         * @return <i>n</i>!
         */
        public static BigInteger Factorial(BigInteger n)
        {
            var result = BigInteger.One;
            while (!n.Equals(BigInteger.Zero))
            {
                result = result.Multiply(n);
                n = n.Subtract(BigInteger.One);
            }

            return result;
        }


        /** Computes the intercept of a polynomial mod N over the integers given a list of points using
         * Lagrangian interpolation.
         * @param points the list of points in the order: <code>(f(1), f(2), f(3),...)</code>
         * @param mod the modulo of the polynomial
         * @return <code>f(0)</code>
         */
        public static BigInteger GetIntercept(List<BigInteger> points, BigInteger mod)
        {
            var k = points.Count;
            var sum = BigInteger.Zero;
            for (var j = 1; j <= k; j++)
            {
                var numerator = 1;
                var denominator = 1;
                for (var m = 1; m <= k; m++)
                {
                    if (m != j)
                    {
                        numerator *= -m;
                        denominator *= j - m;
                    }
                }

                var lambdaj = BigInteger.ValueOf(numerator / denominator);
                sum = sum.Add(points[j - 1].Multiply(lambdaj)).Mod(mod);
            }

            return sum;
        }

        private static readonly int[] JacobiTable = {0, 1, 0, -1, 0, -1, 0, 1};

        public static int Jacobi(BigInteger var0, BigInteger var1)
        {
            var var5 = 1L;
            BigInteger var2;
            if (var1.Equals(BigInteger.Zero))
            {
                var2 = var0.Abs();
                return var2.Equals(BigInteger.One) ? 1 : 0;
            }

            if (!var0.TestBit(0) && !var1.TestBit(0))
            {
                return 0;
            }

            var2 = var0;
            var var3 = var1;
            if (var1.SignValue == -1)
            {
                var3 = var1.Negate();
                if (var0.SignValue == -1)
                    var5 = -1L;
            }

            BigInteger var4;
            for (var4 = BigInteger.Zero; !var3.TestBit(0); var3 = var3.Divide(BigInteger.Two))
                var4 = var4.Add(BigInteger.One);

            if (var4.TestBit(0))
                var5 *= JacobiTable[var0.IntValue & 7];

            if (var0.SignValue < 0)
            {
                if (var3.TestBit(1))
                {
                    var5 = -var5;
                }

                var2 = var0.Negate();
            }

            for (; var2.SignValue != 0; var2 = var2.Subtract(var3))
            {
                for (var4 = BigInteger.Zero; !var2.TestBit(0); var2 = var2.Divide(BigInteger.Two))
                    var4 = var4.Add(BigInteger.One);

                if (var4.TestBit(0))
                    var5 *= JacobiTable[var3.IntValue & 7];

                if (var2.CompareTo(var3) >= 0)
                    continue;
                var var7 = var2;
                var2 = var3;
                var3 = var7;
                if (var2.TestBit(1) && var7.TestBit(1))
                {
                    var5 = -var5;
                }
            }

            return var3.Equals(BigInteger.One) ? (int) var5 : 0;
        }
    }
}