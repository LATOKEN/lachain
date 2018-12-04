using System.Collections.Generic;
using Phorkus.Proto;

namespace Phorkus.Hermes.Generator.State
{
    public abstract class Data<T>
        where T : Data<T>
    {
        protected IReadOnlyDictionary<PublicKey, int> participants;

        protected Data(IReadOnlyDictionary<PublicKey, int> participants) {
            //participants = participants != null ? new Dictionary<PublicKey, int>(participants) : null;
            this.participants = participants;
        }

        /** Returns a new copy of this object with the ActorRef->Party index mapping updated.
         * @param participants the new ActorRef->Party index mapping
         * @return updated structure with a new ActorRef->Party index mapping.
         */
        public abstract T WithParticipants(IReadOnlyDictionary<PublicKey, int> participants);

        public IReadOnlyDictionary<PublicKey, int> GetParticipants() {
            return participants;
        }
    }
}
