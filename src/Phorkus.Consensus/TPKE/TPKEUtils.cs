using System;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using Org.BouncyCastle.Security;
using Phorkus.Crypto.MCL.BLS12_381;

namespace Phorkus.Consensus.TPKE
{
    static class TPKEUtils 
    {
        public static byte[] G(G1 g)
        {
            throw new NotImplementedException();
        }

        public static G2 H(G1 g, byte[] w)
        {
            throw new NotImplementedException();
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