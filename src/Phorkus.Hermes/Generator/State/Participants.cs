using System.Collections.Generic;
using Phorkus.Proto;

namespace Phorkus.Hermes.Generator.State
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
    }
}