using System.Collections.Generic;
using System.IO;
using System.Linq;
using Phorkus.Proto;

namespace Phorkus.Hermes.Generator.Messages
{
    public class Participants
    {
        private IReadOnlyDictionary<PublicKey, int> participants;

        public Participants(IDictionary<PublicKey, int> participants)
        {
            this.participants = new Dictionary<PublicKey, int>(participants);
        }

        /** @return the map containing the mapping between ActorRef's and id in the protocol*/
        public IReadOnlyDictionary<PublicKey, int> GetParticipants()
        {
            return participants;
        }
        
        public Participants(byte[] buffer)
        {
            using (var stream = new MemoryStream(buffer))
            using (var reader = new BinaryReader(stream))
            {
                var CountKeys = participants.Keys.Count();
                
                var pointLength = reader.ReadBytes();
                
                participants = new IReadOnlyDictionary<PublicKey, int>();
            }
        }
        
        public byte[] ToByteArray()
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                var byteArray = participants.ToByteArray();
                writer.Write(byteArray.Length);
                writer.Write(byteArray);
                return stream.ToArray();
            }
        }
    }
}