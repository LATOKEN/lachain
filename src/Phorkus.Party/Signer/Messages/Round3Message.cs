using System.IO;

namespace Phorkus.Party.Signer.Messages
{
    public class Round3Message : ISignerMessage
    {
        public byte[] riWiCommitment;
        
        public Round3Message()
        {
        }
        
        public Round3Message(byte[] riWiCommitment) {
            this.riWiCommitment = riWiCommitment;
        }

        public void fromByteArray(byte[] buffer)
        {
            using (var stream = new MemoryStream(buffer))
            using (var reader = new BinaryReader(stream))
            {
                var len = reader.ReadInt32();
                riWiCommitment = reader.ReadBytes(len);
            }
        }

        public byte[] ToByteArray()
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(riWiCommitment.Length);
                writer.Write(riWiCommitment);
                return stream.ToArray();
            }
        }
    }
}