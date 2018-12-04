using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
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

        public string serialize()
        {
            var riwi = string.Join("Z", openRiWi.Select(value => value.ToString()));
            return riwi + "!" + zkp2.serialize();
        }

        public void deserialize(string s) {
            var arr = s.Split('!');
            openRiWi = new BigInteger[2];
            var values = arr[0].Split('Z');
            openRiWi = new[]
            {
                new BigInteger(values[0]),
                new BigInteger(values[1])
            };
            zkp2 = new Zkpi2();
            zkp2.deserialize(arr[1]);
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