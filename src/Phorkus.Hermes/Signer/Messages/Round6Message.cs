using System.IO;
using Phorkus.Hermes.Crypto;

namespace Phorkus.Hermes.Signer.Messages
{
    public class Round6Message : ISignerMessage
    {
        public PartialDecryption sigmaShare;

        public Round6Message()
        {
        }
        
        public Round6Message(PartialDecryption sigmaShare)
        {
            this.sigmaShare = sigmaShare;
        }

        public void fromByteArray(byte[] buffer)
        {
            using (var memory = new MemoryStream(buffer))
            using (var reader = new BinaryReader(memory))
            {
                var len = reader.ReadInt32();
                var bytes = reader.ReadBytes(len);
                sigmaShare = new PartialDecryption(bytes);
            }
        }

        public byte[] ToByteArray()
        {
            using (var memory = new MemoryStream())
            using (var writer = new BinaryWriter(memory))
            {
                var bytes = sigmaShare.toByteArray();
                writer.Write(bytes.Length);
                writer.Write(bytes);
                return memory.ToArray();
            }
        }
    }
}