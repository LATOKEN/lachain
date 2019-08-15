using System;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using Org.BouncyCastle.Security;
using Phorkus.Crypto;
using Phorkus.Crypto.MCL.BLS12_381;

namespace Phorkus.Consensus.TPKE
{
    static class TPKEUtils 
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
                throw new InvalidParameterException("Byte arrays must have same length.");
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