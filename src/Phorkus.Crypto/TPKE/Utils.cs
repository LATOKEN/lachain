using System.Linq;
using Org.BouncyCastle.Security;
using Phorkus.Crypto.MCL.BLS12_381;

namespace Phorkus.Crypto.TPKE
{
    internal static class Utils 
    {
        public static byte[] G(G1 g)
        {
            return G1.ToBytes(g).Keccak256();
        }

        public static G2 H(G1 g, byte[] w)
        {
            var join = G1.ToBytes(g).Concat(w).ToArray();
            var res = new G2();
            res.SetHashOf(join);
            return res;
        }

        public static byte[] XOR(byte[] lhs, byte[] rhs)
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