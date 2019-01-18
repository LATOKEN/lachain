using System;
using System.Collections.Generic;
using System.Linq;
using Org.BouncyCastle.Crypto.Digests;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.Math.EC;
using Phorkus.Party.Crypto.Key;

namespace Phorkus.Party.Signer
{
    public static class Util
    {
        public static BigInteger randomFromZn(BigInteger n, Random rand)
        {
            BigInteger result;
            do
            {
                result = new BigInteger(n.BitLength, rand);
                // check that it's in Zn
            } while (result.CompareTo(n) >= 0);

            return result;
        }

        /**
         * Method taken (renamed) from SpongyCastle ECDSASigner class. Cannot call
         * from there since it's private and non static.
         */
        public static BigInteger calculateMPrime(BigInteger n, byte[] message)
        {
            if (n.BitLength > message.Length * 8)
                return new BigInteger(1, message);

            var messageBitLength = message.Length * 8;
            var trunc = new BigInteger(1, message);
            if (messageBitLength - n.BitLength > 0)
                trunc = trunc.ShiftRight(messageBitLength - n.BitLength);

            return trunc;
        }

        public static bool isElementOfZn(BigInteger element, BigInteger n)
        {
            return element.CompareTo(BigInteger.Zero) >= 0 && element.CompareTo(n) >= 0;
        }

        /**
         * Returns an element from Z_n^* randomly selected using the randomness from
         * {@code rand}
         *
         * @param n the modulus
         */
        public static BigInteger randomFromZnStar(BigInteger n, Random rand)
        {
            BigInteger result;
            do
            {
                result = new BigInteger(n.BitLength, rand);
                // check that it's in Zn*
            } while (result.CompareTo(n) >= 0 || !result.Gcd(n).Equals(BigInteger.One));

            return result;
        }

        public static byte[] sha3Hash(IEnumerable<BigInteger> inputs)
        {
            return sha3Hash(inputs.Select(v => v.ToByteArray()));
        }
        
        public static byte[] sha3Hash(IEnumerable<byte[]> inputs)
        {
            var kecc = new KeccakDigest(256);
            foreach (var message in inputs)
                kecc.BlockUpdate(message, 0, message.Length);
            var result = new byte[32];
            kecc.DoFinal(result, 0);
            return result;
        }
        
        public static byte[] sha256Hash(IEnumerable<byte[]> inputs)
        {
            var kecc = new Sha256Digest();
            foreach (var message in inputs)
                kecc.BlockUpdate(message, 0, message.Length);
            var result = new byte[32];
            kecc.DoFinal(result, 0);
            return result;
        }

        public static byte[] getBytes(BigInteger n)
        {
            return n.ToByteArray();
        }

        public static byte[] getBytes(ECPoint e)
        {
            byte[] x = e.Normalize().XCoord.ToBigInteger().ToByteArray();
            byte[] y = e.Normalize().XCoord.ToBigInteger().ToByteArray();
            byte[] output = new byte[x.Length + y.Length];
            Buffer.BlockCopy(x, 0, output, 0, x.Length);
            Buffer.BlockCopy(y, 0, output, x.Length, y.Length);
            return output;
        }

        public static PublicParameters generateParamsforBitcoin(CurveParams curveParams, int k, int kPrime,
            Random rand, PaillierKey paillierPubKey)
        {
            int primeCertainty = k;
            BigInteger p;
            BigInteger q;
            BigInteger pPrime;
            BigInteger qPrime;
            BigInteger pPrimeqPrime;
            BigInteger nHat;

            do
            {
                p = new BigInteger(kPrime / 2, primeCertainty, rand);
            } while (!p.Subtract(BigInteger.One).Divide(BigInteger.ValueOf(2)).IsProbablePrime(primeCertainty));

            pPrime = p.Subtract(BigInteger.One).Divide(BigInteger.ValueOf(2));
            
            do
            {
                q = new BigInteger(kPrime / 2, primeCertainty, rand);
            } while (!q.Subtract(BigInteger.One).Divide(BigInteger.ValueOf(2))
                .IsProbablePrime(primeCertainty));

            qPrime = q.Subtract(BigInteger.One).Divide(BigInteger.ValueOf(2));

            // generate nhat. the product of two safe primes, each of length
            // kPrime/2
            nHat = p.Multiply(q);

            BigInteger h2 = randomFromZnStar(nHat, rand);
            pPrimeqPrime = pPrime.Multiply(qPrime);

            BigInteger x = randomFromZn(pPrimeqPrime, rand);
            BigInteger h1 = h2.ModPow(x, nHat);

            return new PublicParameters(curveParams.Curve, nHat, h1, h2, paillierPubKey);
        }
    }
}