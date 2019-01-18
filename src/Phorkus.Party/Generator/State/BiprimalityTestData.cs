using System;
using System.Collections.Generic;
using System.Linq;
using Org.BouncyCastle.Math;
using Phorkus.Proto;
using Phorkus.Utility;

namespace Phorkus.Party.Generator.State
{
    public class BiprimalityTestData : Data<BiprimalityTestData>
    {
        private IDictionary<PublicKey, BigInteger>[] Qs;

        /** Current candidate to RSA modulus*/
        public BigInteger N;

        /** Round number in the Biprimality test*/
        public readonly int round;

        /** BGW private parameters associated with the current candidate to RSA modulus*/
        public readonly BgwPrivateParams bgwPrivateParameters;


        private BiprimalityTestData(IReadOnlyDictionary<PublicKey, int> participants,
            BigInteger N,
            BgwPrivateParams bgwPrivateParameters,
            IReadOnlyList<IDictionary<PublicKey, BigInteger>> Qs,
            int round)
            : base(participants)
        {
            this.Qs = new IDictionary<PublicKey, BigInteger>[2];
            for (var i = 0; i < Qs.Count; i++)
                this.Qs[i] = Qs[i];
            this.N = N;
            this.bgwPrivateParameters = bgwPrivateParameters;
            this.round = round;
        }

        public static BiprimalityTestData init()
        {
            var Qs = new IDictionary<PublicKey, BigInteger>[2];
            Qs[0] = new SortedDictionary<PublicKey, BigInteger>(new PublicKeyComparer());
            Qs[1] = new SortedDictionary<PublicKey, BigInteger>(new PublicKeyComparer());
            return new BiprimalityTestData(null, null, null, Qs, 0);
        }

        public bool hasQiOf(IEnumerable<PublicKey> isy, int round)
        {
            if (isy == null)
                throw new ArgumentNullException(nameof(isy));
            return isy.All(i => Qs[round % 2].ContainsKey(i));
        }

        public IEnumerable<KeyValuePair<PublicKey, BigInteger>> qis(int round)
        {
            return Qs[round % 2];
        }

        public IDictionary<PublicKey, BigInteger> qiss(int round)
        {
            return Qs[round % 2];
        }

        public BiprimalityTestData withNewCandidateN(BigInteger N, BgwPrivateParams bgwPrivateParameters)
        {
            return new BiprimalityTestData(participants, N, bgwPrivateParameters, Qs, round);
        }

        public BiprimalityTestData withNewQi(BigInteger Qi, PublicKey fromId, int round)
        {
            if (Qs[round % 2].ContainsKey(fromId))
                return this;
            var qs = Qs[round % 2];
            qs.Add(fromId, Qi);
            return new BiprimalityTestData(participants, N, bgwPrivateParameters, Qs, round);
        }

        public override BiprimalityTestData WithParticipants(IReadOnlyDictionary<PublicKey, int> participants)
        {
            return new BiprimalityTestData(participants, N, bgwPrivateParameters, Qs, round);
        }

        public BiprimalityTestData forNextRound()
        {
            var newQs = new Dictionary<PublicKey, BigInteger>[2];
            newQs[0] = new Dictionary<PublicKey, BigInteger>();
            newQs[1] = new Dictionary<PublicKey, BigInteger>();
            newQs[round % 2] = new Dictionary<PublicKey, BigInteger>();
            newQs[(round + 1) % 2] = new Dictionary<PublicKey, BigInteger>(Qs[(round + 1) % 2]);
            return new BiprimalityTestData(participants, N, bgwPrivateParameters, newQs, round + 1);
        }

        public BiprimalityTestData forNextCandidate()
        {
            return init().WithParticipants(participants);
        }
    }
}