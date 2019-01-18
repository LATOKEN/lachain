using System.IO;
using Org.BouncyCastle.Math;

namespace Phorkus.Party.Generator.Messages
{
    public class BGWNPoint
    {
        /**A share of N*/
        public BigInteger point;

        public BGWNPoint(BigInteger point)
        {
            this.point = point;
        }

        public BGWNPoint(byte[] buffer)
        {
            using (var stream = new MemoryStream(buffer))
            using (var reader = new BinaryReader(stream))
            {
                var pointLength = reader.ReadInt32();
                point = new BigInteger(reader.ReadBytes(pointLength));
            }
        }
        
        public byte[] ToByteArray()
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                var byteArray = point.ToByteArray();
                writer.Write(byteArray.Length);
                writer.Write(byteArray);
                return stream.ToArray();
            }
        }
    }
}