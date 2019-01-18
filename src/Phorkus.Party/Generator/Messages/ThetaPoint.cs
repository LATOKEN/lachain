using System.IO;
using Org.BouncyCastle.Math;

namespace Phorkus.Party.Generator.Messages
{
    public class ThetaPoint
    {
        /**a share of Theta*/
        public BigInteger thetai;

        public ThetaPoint(BigInteger thetai)
        {
            this.thetai = thetai;
        }
        
        public ThetaPoint(byte[] buffer)
        {
            using (var stream = new MemoryStream(buffer))
            using (var reader = new BinaryReader(stream))
            {
                var thetaiLength = reader.ReadInt32();
                thetai = new BigInteger(reader.ReadBytes(thetaiLength));
            }
        }
        
        public byte[] ToByteArray()
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                var byteArray = thetai.ToByteArray();
                writer.Write(byteArray.Length);
                writer.Write(byteArray);
                return stream.ToArray();
            }
        }
    }
}