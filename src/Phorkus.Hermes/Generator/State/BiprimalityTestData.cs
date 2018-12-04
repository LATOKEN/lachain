using System;
using System.Collections.Generic;
using System.Linq;
using Org.BouncyCastle.Math;
using Phorkus.Proto;

namespace Phorkus.Hermes.Generator.State
{
    public class BiprimalityTestData : Data<BiprimalityTestData>
    {
        private Dictionary<int, BigInteger>[] Qs;

        /** Current candidate to RSA modulus*/
        public BigInteger N;

        /** Round number in the Biprimality test*/
        public readonly int round;

        /** BGW private parameters associated with the current candidate to RSA modulus*/
        public readonly BgwPrivateParams bgwPrivateParameters;


        private BiprimalityTestData(IReadOnlyDictionary<PublicKey, int> participants,
            BigInteger N,
            BgwPrivateParams bgwPrivateParameters,
            IDictionary<int, BigInteger>[] Qs,
            int round)
            : base(participants)
        {
            this.Qs = new Dictionary<int, BigInteger>[2];

            for (var i = 0; i < Qs.Length; i++)
                this.Qs[i] = new Dictionary<int, BigInteger>(Qs[i]);

            this.N = N;
            this.bgwPrivateParameters = bgwPrivateParameters;
            this.round = round;
        }

        public static BiprimalityTestData init()
        {
            Dictionary<int, BigInteger>[] Qs = new Dictionary<int, BigInteger>[2];
            Qs[0] = new Dictionary<int, BigInteger>();
            Qs[1] = new Dictionary<int, BigInteger>();
            return new BiprimalityTestData(null, null, null, Qs, 0);
        }

        public bool hasQiOf(IEnumerable<int> isy, int round)
        {
            if (isy == null)
                throw new ArgumentNullException(nameof(isy));
            return isy.All(i => Qs[round % 2].ContainsKey(i));
        }

        public IEnumerable<KeyValuePair<int, BigInteger>> qis(int round)
        {
            return Qs[round % 2];
        }

        public IDictionary<int, BigInteger> qiss(int round)
        {
            return Qs[round % 2];
        }

        public BiprimalityTestData withNewCandidateN(BigInteger N,
            BgwPrivateParams bgwPrivateParameters)
        {
            return new BiprimalityTestData(participants, N, bgwPrivateParameters, Qs, round);
        }

        public BiprimalityTestData withNewQi(BigInteger Qi, int fromId, int round)
        {
            if (Qs[round % 2].ContainsKey(fromId))
                return this;

            IDictionary<int, BigInteger> newMap = new Dictionary<int, BigInteger>(Qs[round % 2]);
            newMap.Add(fromId, Qi);

            IDictionary<int, BigInteger>[] newQs = new IDictionary<int, BigInteger>[2];
            newQs[round % 2] = newMap;
            newQs[(round + 1) % 2] = Qs[(round + 1) % 2];

            return new BiprimalityTestData(participants, N, bgwPrivateParameters, newQs, round);
        }

        public override BiprimalityTestData WithParticipants(IReadOnlyDictionary<PublicKey, int> participants)
        {
            return new BiprimalityTestData(participants, N, bgwPrivateParameters, Qs, round);
        }

        public BiprimalityTestData forNextRound()
        {
            Dictionary<int, BigInteger>[] newQs = new Dictionary<int, BigInteger>[2];
            newQs[0] = new Dictionary<int, BigInteger>();
            newQs[1] = new Dictionary<int, BigInteger>();

            newQs[round % 2] = new Dictionary<int, BigInteger>();
            newQs[(round + 1) % 2] = new Dictionary<int, BigInteger>(Qs[(round + 1) % 2]);
            return new BiprimalityTestData(participants, N, bgwPrivateParameters, newQs, round + 1);
        }

        public BiprimalityTestData forNextCandidate()
        {
            return init().WithParticipants(participants);
        }
    }
}