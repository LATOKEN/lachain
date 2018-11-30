using System.IO;
using Org.BouncyCastle.Math;

namespace Phorkus.Hermes.Generator.Messages
{
    public class VerificationKey
    {
        /** A verification key*/
        public BigInteger verificationKey;

        public VerificationKey(BigInteger verificationKey)
        {
            this.verificationKey = verificationKey;
        }
        
        public VerificationKey(byte[] buffer)
        {
            using (var stream = new MemoryStream(buffer))
            using (var reader = new BinaryReader(stream))
            {
                var verificationKeyLength = reader.ReadInt32();
                verificationKey = new BigInteger(reader.ReadBytes(verificationKeyLength));
            }
        }
        
        public byte[] ToByteArray()
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                var byteArray = verificationKey.ToByteArray();
                writer.Write(byteArray.Length);
                writer.Write(byteArray);
                return stream.ToArray();
            }
        }
    }
}