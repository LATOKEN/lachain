using System.IO;

namespace Phorkus.Party.Generator.Messages
{
    public class ComplaintMessage
    {
        /** The id of the party that produced the invalid share*/
        public int id;

        public ComplaintMessage(int id)
        {
            this.id = id;
        }
        
        public ComplaintMessage(byte[] buffer)
        {
            using (var stream = new MemoryStream(buffer))
            using (var reader = new BinaryReader(stream))
            {
                id = reader.ReadInt32();
            }
        }
        
        public byte[] ToByteArray()
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(id);
                return stream.ToArray();
            }
        }
    }
}