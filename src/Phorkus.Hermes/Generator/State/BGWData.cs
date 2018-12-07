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
    public class BGWData : Data<BGWData>
    {
        /** Collection of the recieved shares of N*/
        public IDictionary<PublicKey, BigInteger> Ns;

        /** The BGW private parameters selected by the actor in the BGW protocol (p<sub>i</sub>, q<sub>i</sub>, ...)*/
        public BgwPrivateParams bgwPrivateParameters;

        public IDictionary<PublicKey, BgwPublicParams> bgwPublicParameters;

        private BGWData(IReadOnlyDictionary<PublicKey, int> participants,
            BgwPrivateParams bgwPrivateParameters,
            IDictionary<PublicKey, BgwPublicParams> bgwPublicParameters,
            IDictionary<PublicKey, BigInteger> Ns)
            : base(participants)
        {
            this.bgwPrivateParameters = bgwPrivateParameters;
            this.bgwPublicParameters = bgwPublicParameters;
            this.Ns = Ns;
        }

        public static BGWData Init(IComparer<PublicKey> publicKeyComparer)
        {
            return new BGWData(null, null, new SortedDictionary<PublicKey, BgwPublicParams>(publicKeyComparer),
                new SortedDictionary<PublicKey, BigInteger>(publicKeyComparer));
        }

        public bool HasShareOf(IEnumerable<PublicKey> var)
        {
            return var.All(i => bgwPublicParameters.ContainsKey(i));
        }

        public IEnumerable<KeyValuePair<PublicKey, BgwPublicParams>> shares()
        {
            return bgwPublicParameters;
        }

        public bool hasNiOf(IEnumerable<PublicKey> isy)
        {
            return isy.All(i => Ns.ContainsKey(i));
        }

        public IEnumerable<KeyValuePair<PublicKey, BigInteger>> nis()
        {
            return Ns;
        }

        public override BGWData WithParticipants(IReadOnlyDictionary<PublicKey, int> participants)
        {
            return new BGWData(participants, bgwPrivateParameters, bgwPublicParameters, Ns);
        }

        public BGWData WithPrivateParameters(BgwPrivateParams param)
        {
            return new BGWData(participants, param, bgwPublicParameters, Ns);
        }

        public BGWData WithNewShare(BgwPublicParams share, PublicKey fromId)
        {
            if (bgwPublicParameters.ContainsKey(fromId))
                return this;
            bgwPublicParameters.Add(fromId, share);
            return new BGWData(participants, bgwPrivateParameters, bgwPublicParameters, Ns);
        }

        public BGWData withNewNi(BigInteger Ni, PublicKey fromId)
        {
            if (Ns.ContainsKey(fromId))
                return this;
            Ns.Add(fromId, Ni);
            return new BGWData(participants, bgwPrivateParameters, bgwPublicParameters, Ns);
        }

        // IDictionary<IActorRef, int> 
        public BGWData withCandidateN(BigInteger candidateN)
        {
            return new BGWData(participants, bgwPrivateParameters, bgwPublicParameters, Ns);
        }
    }
}