using System.Collections.Generic;
using System.IO;
using System.Linq;
using Org.BouncyCastle.Math;

namespace Phorkus.Hermes.Signer.Messages
{
    public class Round4Message : ISignerMessage
    {
        public IEnumerable<BigInteger> openRiWi;
        public Zkpi2 zkp2;

        public Round4Message() {
        }

        public Round4Message(IEnumerable<BigInteger> openRiWi, Zkpi2 zkp2) {
            this.openRiWi = openRiWi;
            this.zkp2 = zkp2;
        }

        public void fromByteArray(byte[] buffer)
        {
            using (var stream = new MemoryStream(buffer))
            using (var reader = new BinaryReader(stream))
            {
                var glen = reader.ReadInt32();
                var bigs = new BigInteger [glen];
                for (var i = 0; i < glen; i++)
                {
                    var len = reader.ReadInt32();
                    bigs[i] = new BigInteger(reader.ReadBytes(len));
                }
                openRiWi = bigs;
                var bytes = reader.ReadString();
                zkp2 = new Zkpi2();
                zkp2.deserialize(bytes);
            }
        }

        public byte[] ToByteArray()
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                var values = openRiWi.ToArray();
                writer.Write(values.Length);
                foreach (var value in values)
                {
                    var bytes = value.ToByteArray();
                    writer.Write(bytes.Length);
                    writer.Write(bytes);
                }
                var zkp = zkp2.serialize();
                writer.Write(zkp);
                return stream.ToArray();
            }
        }
    }
}