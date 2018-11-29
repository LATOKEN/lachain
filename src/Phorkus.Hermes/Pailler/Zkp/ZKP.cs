using System;
using System.IO;
using System.Security.Cryptography;
using Org.BouncyCastle.Math;

namespace Phorkus.Hermes.Pailler.Zkp
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

        protected BigInteger hash(params object[] byteArrays)
        {
            if (byteArrays.Length == 0)
            {
                throw new ArgumentException("You must supply at least one array");
            }

            using (var stream = new MemoryStream())
            {
                foreach (var bytes in byteArrays)
                    stream.Write((byte[]) bytes, 0, ((byte[]) bytes).Length);
                stream.Write((byte[]) byteArrays[byteArrays.Length - 1], 0, ((byte[]) byteArrays[byteArrays.Length - 1]).Length);
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