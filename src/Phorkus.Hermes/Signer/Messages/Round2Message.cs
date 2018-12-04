using System.Collections.Generic;
using System.IO;
using System.Linq;
using Org.BouncyCastle.Math;

namespace Phorkus.Hermes.Signer.Messages
{
    public class Round2Message : ISignerMessage
    {
        public IEnumerable<BigInteger> openUiVi;
        public Zkpi1 zkp1;

        public Round2Message()
        {
        }
        
        public Round2Message(IEnumerable<BigInteger> openUiVi, Zkpi1 zkp1)
        {
            this.openUiVi = openUiVi;
            this.zkp1 = zkp1;
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
                    bigs[i] = new BigInteger((reader.ReadBytes(len)).ToArray());
                }
                openUiVi = bigs;
                var zlen = reader.ReadInt32();
                var bytes = reader.ReadBytes(zlen);
                zkp1 = new Zkpi1();
                zkp1.fromByteArray(bytes);
            }
        }

        public byte[] ToByteArray()
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                var values = openUiVi.ToArray();
                writer.Write(values.Length);
                foreach (var value in values)
                {
                    var bytes = value.ToByteArray();
                    writer.Write(bytes.Length);
                    writer.Write(bytes);
                }

                var zkp = zkp1.ToByteArray();
                writer.Write(zkp.Length);
                writer.Write(zkp);
                return stream.ToArray();
            }
        }
    }
}