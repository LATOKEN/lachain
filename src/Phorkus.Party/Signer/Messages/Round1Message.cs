using System.IO;

namespace Phorkus.Party.Signer.Messages
{
    public class Round1Message : ISignerMessage
    {
        public byte[] uIviCommitment;

        public Round1Message()
        {
        }
        
        public Round1Message(byte[] uIviCommitment) {
            this.uIviCommitment = uIviCommitment;
        }

        public void fromByteArray(byte[] buffer)
        {
            using (var stream = new MemoryStream(buffer))
            using (var reader = new BinaryReader(stream))
            {
                var len = reader.ReadInt32();
                uIviCommitment = reader.ReadBytes(len);
            }
        }

        public byte[] ToByteArray()
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(uIviCommitment.Length);
                writer.Write(uIviCommitment);
                return stream.ToArray();
            }
        }
    }
}