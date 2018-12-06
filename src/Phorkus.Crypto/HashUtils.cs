using System;
using System.Text;
using Org.BouncyCastle.Crypto.Digests;

namespace Phorkus.Crypto
{
    public static class HashUtils
    {
        public static byte[] Sha256(this string message)
        {
            return Encoding.UTF8.GetBytes(message).Sha256();
        }
        
        public static byte[] Sha256(this byte[] message, int offset, int count)
        {
            var hash = new Sha256Digest();
            hash.BlockUpdate(message, offset, count);
           
            var result = new byte[32];
            hash.DoFinal(result, 0);
            return result;
        }
        
        public static byte[] Sha256(this byte[] message)
        {
            return Sha256(message, 0, message.Length);
        }
        
        public static byte[] Ripemd160(this string message)
        {
            return Encoding.ASCII.GetBytes(message).Ripemd160();
        }
        
        public static byte[] Ripemd160(this byte[] message, int offset, int count)
        {
            var hash = new RipeMD160Digest();
            hash.BlockUpdate(message, offset, count);
            var result = new byte[20];
            hash.DoFinal(result, 0);
            return result;
        }

        public static byte[] Ripemd160(this byte[] message)
        {
            return Ripemd160(message, 0, message.Length);
        }

        public static byte[] Ed25519(this byte[] message)
        {
            throw new NotImplementedException();
        }
        
        public static byte[] Murmur3(this byte[] message, uint seed)
        {
            using (var murmur = new Murmur3(seed))
                return murmur.ComputeHash(message);
        }

        public static uint Murmur32(this byte[] message, uint seed)
        {
            using (var murmur = new Murmur3(seed))
            {
                return BitConverter.ToUInt32(murmur.ComputeHash(message), 0);
            }
        }
    }
}