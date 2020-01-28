using System.Collections.Generic;
using System.Linq;
using Org.BouncyCastle.Crypto.Digests;
using Org.BouncyCastle.Crypto.Prng;
using Org.BouncyCastle.Security;
using Phorkus.Crypto.MCL.BLS12_381;

namespace Phorkus.Crypto.TPKE
{
    internal static class Utils 
    {
        public static byte[] XorWithHash(G1 g, byte[] data)
        {
            var prng = new DigestRandomGenerator(new Sha3Digest());
            prng.AddSeedMaterial(G1.ToBytes(g));
            var pseudoRandomBytes = new byte[data.Length];
            prng.NextBytes(pseudoRandomBytes);
            return Xor(pseudoRandomBytes, data);
        }

        public static G2 HashToG2(G1 g, IEnumerable<byte> w)
        {
            var join = G1.ToBytes(g).Concat(w).ToArray();
            var res = new G2();
            res.SetHashOf(join);
            return res;
        }

        private static byte[] Xor(byte[] lhs, byte[] rhs)
        {
            if (lhs.Length != rhs.Length)
            {
                throw new InvalidParameterException($"Byte arrays must have same length but got {lhs.Length} vs {rhs.Length}.");
            }
            
            byte[] res = new byte[lhs.Length];
            for (var i = 0; i < lhs.Length; ++i)
            {
                res[i] = (byte) (lhs[i] ^ rhs[i]); 
            }

            return res;
        }
    }
}