using System;
using System.Linq;
using System.Numerics;
using Google.Protobuf;
using Lachain.Utility.Utils;
using Nethereum.Util;

namespace Lachain.Crypto.VRF
{
    public static class Vrf
    {
        public static (byte[], byte[], BigInteger) Evaluate(byte[] privateKey, byte[] seed, byte[] role, BigInteger tau,
            BigInteger w, BigInteger W)
        {
            var prv = privateKey.ToHex(false);
            var message = Combine(seed, role).ToHex(false);
            var proof = VrfImports.evaluate(prv, message).HexToBytes();
            var value = VrfImports.proof_to_hash(proof.ToHex(false)).HexToBytes();
            var j = Sortition.GetVotes(value, w, tau, W); 
            return (proof, value, j);
        }
        
        public static bool IsWinner(byte[] publicKey, byte[] proofBytes, byte[] seed, byte[] role, BigInteger tau,
            BigInteger w, BigInteger W)
        {
            var pub = publicKey.ToHex(false);
            var proof = proofBytes.ToHex(false);
            var value = ProofToHash(proofBytes);
            var message = Combine(seed, role).ToHex(false);
            var result = VrfImports.verify(pub, proof, message);
            if (!result)
            {
                return false;
            }
            var j = Sortition.GetVotes(value, w, tau, W);
            return j > 0;
        }

        public static byte[] ProofToHash(byte[] proof)
        {
            return VrfImports.proof_to_hash(proof.ToHex(false)).HexToBytes();
        } 
    
        private static byte[] Combine(params byte[][] arrays)
        {
            byte[] rv = new byte[arrays.Sum(a => a.Length)];
            int offset = 0;
            foreach (byte[] array in arrays) {
                Buffer.BlockCopy(array, 0, rv, offset, array.Length);
                offset += array.Length;
            }
            return rv;
        }
    }
}