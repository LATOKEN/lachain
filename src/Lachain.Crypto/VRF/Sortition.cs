using System;
using System.Numerics;
using Nethereum.Hex.HexConvertors.Extensions;

namespace Lachain.Crypto.VRF
{
    public static class Sortition
    {
        
        public static BigInteger GetVotes(byte[] hash, BigInteger w, BigInteger tau, BigInteger W) {
            var hashValue = hash.ToHex().HexToBigInteger(false);
            var maxValue = BigInteger.Pow(2, 256);
            var value = hashValue * BigInteger.Pow(10, 18) / maxValue;
            var j = 0;
            var curr = Cumulative(j, w, tau, W);
            while (j <= w && value >= curr) {
                j++;
                var next = Cumulative(j, w, tau, W);
                if (value < next) {
                    return j;
                }
                curr = next;
            }
            return 0;
        }

        private static BigInteger Cumulative(BigInteger j, BigInteger w, BigInteger tau, BigInteger W)
        {
            var sum = new BigInteger(0);
            for (var i = 0; i <= j; i++)
            {
                var (a1, a2) = Binomial(i, w, tau, W);
                sum += a1 * BigInteger.Pow(10, 18) / a2;
            }
            
            return sum;
        }

        private static (BigInteger, BigInteger) Binomial(BigInteger k, BigInteger w, BigInteger tau, BigInteger W)
        {
            
            var (a1, a2) = Combination(w, k);
            var b1 = BigInteger.Pow(tau, (int)k);
            var b2 = BigInteger.Pow(W, (int)k);
            // require W > tau
            var c1 = BigInteger.Pow(W - tau, (int) (w - k));
            var c2 = BigInteger.Pow(W, (int) (w - k));

            return (a1 * b1  * c1, a2 * c2 * b2);
        }
        
        private static (BigInteger, BigInteger) Combination(BigInteger m, BigInteger n) {
            return (Factorial(m, n), (Factorial(n, n)));
        }

        private static BigInteger Factorial(BigInteger m, BigInteger n)
        {
            var num = new BigInteger(1);
            var count = 0;
            for (var i = m; i > 0; i--) {
                if (count == n) {
                    break;
                }
                num = num * i;
                count++;
            }

            return num;
        }
    }
}