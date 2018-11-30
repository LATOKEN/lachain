using System;
using System.IO;
using System.Security.Cryptography;
using Org.BouncyCastle.Math;

namespace Phorkus.Hermes.Crypto.Zkp
{
    public abstract class ZKP
    {
        protected readonly HashAlgorithm HashFunction;
        
        public ZKP()
            : this("SHA-1")
        {
        }

        public ZKP(String hashFunctionName)
        {
            
            HashFunction = HashAlgorithm.Create(hashFunctionName);
        }

        protected BigInteger hash(params byte[][] byteArrays)
        {
            if (byteArrays.Length == 0)
            {
                throw new ArgumentException("You must supply at least one array");
            }

            using (var stream = new MemoryStream())
            {
                foreach (var bytes in byteArrays)
                    stream.Write(bytes, 0, bytes.Length);
                stream.Write(byteArrays[byteArrays.Length - 1], 0, byteArrays[byteArrays.Length - 1].Length);
                var hash = HashFunction.ComputeHash(stream.ToArray());
                /* TODO: "be careful with biginteger's sign here" */
                return new BigInteger(hash);
            }
        }

        public abstract bool Verify();

        public abstract byte[] toByteArray();

        public abstract byte[] toByteArrayNoKey();

        public abstract BigInteger getValue();
    }
}