using System.Collections.Generic;
using Phorkus.Proto;

namespace Phorkus.Hermes.Generator.State
{
    public abstract class AbstractData<T>
    {
        protected IReadOnlyDictionary<PublicKey, int> Participants;

        protected AbstractData(IReadOnlyDictionary<PublicKey, int> participants) {
            //Participants = participants != null ? new Dictionary<PublicKey, int>(participants) : null;
            Participants = participants;
        }

        /** Returns a new copy of this object with the ActorRef->Party index mapping updated.
         * @param Participants the new ActorRef->Party index mapping
         * @return updated structure with a new ActorRef->Party index mapping.
         */
        public abstract T WithParticipants(IReadOnlyDictionary<PublicKey, int> participants);

        public IReadOnlyDictionary<PublicKey, int> GetParticipants() {
            return Participants;
        }
    }
}
