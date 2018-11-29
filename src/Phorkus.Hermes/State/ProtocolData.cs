using System.Collections.Generic;
using Org.BouncyCastle.Math;
using Phorkus.Hermes.Config;
using Phorkus.Proto;

namespace Phorkus.Hermes.State
{
    public class ProtocolData : AbstractData<ProtocolData>
    {
        /** The current candidate to RSA modulus in the protocol. Can be accepted or not depending on the phase.*/
        public BigInteger N;

        /** The BGW private parameters associated with the current N.*/
        public BgwPrivateParams bgwPrivateParameters;

        private ProtocolData(IReadOnlyDictionary<PublicKey, int> participants,
            BigInteger N,
            BgwPrivateParams bgwPrivateParameters)
            : base(participants)
        {
            this.N = N;
            this.bgwPrivateParameters = bgwPrivateParameters;
        }

        public ProtocolData WithNewN(BigInteger N, BgwPrivateParams bgwPrivateParameters)
        {
            return new ProtocolData(Participants, N, bgwPrivateParameters);
        }

        /** Used to initialize the data object.
         * @return  a new object with all the field initialized to null
         */
        public static ProtocolData init()
        {
            return new ProtocolData(null, null, null);
        }
        
        public override ProtocolData WithParticipants(IReadOnlyDictionary<PublicKey, int> participants)
        {
            return new ProtocolData(participants, N, bgwPrivateParameters);
        }
    }
}
