using System.IO;
using Phorkus.Hermes.Crypto;

namespace Phorkus.Hermes.Signer.Messages
{
    public class Round5Message : ISignerMessage
    {
        public PartialDecryption wShare;

        public Round5Message()
        {
        }
        
        public Round5Message(PartialDecryption wShare) {
            this.wShare = wShare;
        }

        public void fromByteArray(byte[] buffer)
        {
            using (var memory = new MemoryStream(buffer))
            using (var reader = new BinaryReader(memory))
            {
                var len = reader.ReadInt32();
                var bytes = reader.ReadBytes(len);
                wShare = new PartialDecryption(bytes);
            }
        }

        public byte[] ToByteArray()
        {
            using (var memory = new MemoryStream())
            using (var writer = new BinaryWriter(memory))
            {
                var bytes = wShare.toByteArray();
                writer.Write(bytes.Length);
                writer.Write(bytes);
                return memory.ToArray();
            }
        }
    }
}