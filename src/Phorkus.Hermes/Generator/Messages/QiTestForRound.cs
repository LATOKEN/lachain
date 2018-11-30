using System.IO;
using Org.BouncyCastle.Math;

namespace Phorkus.Hermes.Generator.Messages
{
    public class QiTestForRound
    {
        /** A Qi in the Biprimality test*/
        public BigInteger Qi;

        /** The current round number in the Biprimality test*/
        public int round;

        public QiTestForRound(BigInteger Qi, int round)
        {
            this.Qi = Qi;
            this.round = round;
        }
        
        public QiTestForRound(byte[] buffer)
        {
            using (var stream = new MemoryStream(buffer))
            using (var reader = new BinaryReader(stream))
            {
                var qiLength = reader.ReadInt32();
                Qi = new BigInteger(reader.ReadBytes(qiLength));
                round = reader.ReadInt32();
            }
        }
        
        public byte[] ToByteArray()
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                var byteArray = Qi.ToByteArray();
                writer.Write(byteArray.Length);
                writer.Write(byteArray);
                
                var byteArrayRound = round;
                writer.Write(byteArrayRound);
                return stream.ToArray();
            }
        }
    }
}