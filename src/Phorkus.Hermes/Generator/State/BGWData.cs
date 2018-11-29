using System.Collections.Generic;
using System.Linq;
using Org.BouncyCastle.Math;
using Phorkus.Proto;

namespace Phorkus.Hermes.Generator.State
{
/** Represents the state data of the BGW protocol Actor's FSM.
 * <p>
 * This is an immutable object type in order to comply to the Akka good practices regarding FSMs.
 * @author Christian Mouchet
 */
    public class BGWData : AbstractData<BGWData>
    {
        
        /** Collection of the recieved shares of N*/
        public IDictionary<int, BigInteger> Ns;
        
        /** The BGW private parameters selected by the actor in the BGW protocol (p<sub>i</sub>, q<sub>i</sub>, ...)*/
        public BgwPrivateParams bgwPrivateParameters;

        public IDictionary<int, BgwPublicParams> bgwPublicParameters;

        private BGWData(IReadOnlyDictionary<PublicKey, int> participants,
            BgwPrivateParams bgwPrivateParameters,
            IDictionary<int, BgwPublicParams> bgwPublicParameters,
            IDictionary<int, BigInteger> Ns)
            : base(participants)
        {
            this.bgwPrivateParameters = bgwPrivateParameters;
            this.bgwPublicParameters = new Dictionary<int, BgwPublicParams>(bgwPublicParameters);
            this.Ns = new Dictionary<int, BigInteger>(Ns);
        }

        public static BGWData Init()
        {
            return new BGWData(null,
                null,
                new Dictionary<int, BgwPublicParams>(),
                new Dictionary<int, BigInteger>());
        }

        public bool HasShareOf(ICollection<int> var)
        {
            return var.All(i => bgwPublicParameters.ContainsKey(i));
        }

        public IEnumerable<KeyValuePair<int, BgwPublicParams>> shares()
        {
            return bgwPublicParameters;
        }

        public bool hasNiOf(ICollection<int> isy)
        {
            return isy.All(i => Ns.ContainsKey(i));
        }

        public IEnumerable<KeyValuePair<int, BigInteger>> nis()
        {
            // return Ns.entrySet().stream();
            return Ns;
        }

        public override BGWData WithParticipants(IReadOnlyDictionary<PublicKey, int> participants)
        {
            return new BGWData(participants,
                bgwPrivateParameters,
                bgwPublicParameters,
                Ns);
        }

        public BGWData withPrivateParameters(BgwPrivateParams param)
        {
            return new BGWData(Participants, param, bgwPublicParameters, Ns);
        }

        public BGWData withNewShare(BgwPublicParams share, int fromId)
        {
            if (bgwPublicParameters.ContainsKey(fromId))
                return this;

            IDictionary<int, BgwPublicParams> newMap =
                new Dictionary<int, BgwPublicParams>(bgwPublicParameters);
            newMap.Add(fromId, share);
            return new BGWData(Participants, bgwPrivateParameters, newMap, Ns);
        }

        public BGWData withNewNi(BigInteger Ni, int fromId)
        {
            if (Ns.ContainsKey(fromId))
                return this;

            IDictionary<int, BigInteger> newNs = new Dictionary<int, BigInteger>(Ns);
            newNs.Add(fromId, Ni);
            return new BGWData(Participants, bgwPrivateParameters, bgwPublicParameters, newNs);
        }

        // IDictionary<IActorRef, int> 
        public BGWData withCandidateN(BigInteger candidateN)
        {
            return new BGWData(Participants, bgwPrivateParameters, bgwPublicParameters, Ns);
        }
    }
}