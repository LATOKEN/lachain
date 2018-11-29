using System.Collections.Generic;
using Org.BouncyCastle.Math;
using Phorkus.Hermes.Generator.State;
using Phorkus.Proto;

namespace Phorkus.Hermes.Generator.Network
{
    public class Messages
    {
              /*
         * EXTERNAL MESSAGES 
         */

        /**
         * A complaint message sent when a share was found to be invalid.
         */
        
        public class Complaint
        {
            /** The id of the party that produced the invalid share*/
            public int id;

            public Complaint(int id)
            {
                this.id = id;
            }
        }


        /**
         * Wraps a BigInteger as a share of N
         */
        
        public class BGWNPoint
        {
            /**A share of N*/
            public BigInteger point;

            public BGWNPoint(BigInteger point)
            {
                this.point = point;
            }
        }


        /**
         * Wraps a BigInteger as a share of Theta
         */
        
        public class ThetaPoint
        {
            /**a share of Theta*/
            public BigInteger thetai;

            public ThetaPoint(BigInteger thetai)
            {
                this.thetai = thetai;
            }
        }

        /**
         * Wraps a BigInteger as a verification key
         */
        
        public class VerificationKey
        {
            /** A verification key*/
            public BigInteger verificationKey;

            public VerificationKey(BigInteger verificationKey)
            {
                this.verificationKey = verificationKey;
            }
        }

        /** 
         * Wraps a BigInteger as a Qi used for Biprimality test. It also contains the round number to avoid concurrency issues 
         */
        
        public class QiTestForRound
        {
            /** A Qi in the Biprimality test*/
            public BigInteger Qi;

            /** The current round number in the Biprimality test*/
            public int round;

            public QiTestForRound(BigInteger Qi, int round)
            {
                this.Qi = Qi;
                this.round = round;
            }
        }


        /**
         * Wraps an HashMap containing the mapping between ActorRef's and id in the protocol
         */
        
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

        /*
         * INTERNAL MESSAGES 
         */

        /**
         * Wraps a BigInteger as a candidate to RSA modulus and its associated BGW private parameters
         */
        
        public class CandidateN
        {
            /** The candidate to RSA modulus*/
            public BigInteger N;

            /** The BGW private parameters associated with the candidate to RSA modulus*/
            public BgwPrivateParams BgwPrivateParameters;

            public CandidateN(BigInteger candidateN, BgwPrivateParams bgwPrivateParameters)
            {
                N = candidateN;
                this.BgwPrivateParameters = bgwPrivateParameters;
            }
        }

        /**
         *	Wraps a BigInteger as a tested candidate to RSA modulus along with its associated BGW private parameters and
         *	a boolean indicating success or failure of the Biprimality test.
         */
        
        public class BiprimalityTestResult
        {
            /** The candidate to RSA modulus*/
            public BigInteger N;

            /** The BGW private parameters associated with N*/
            public BgwPrivateParams BgwPrivateParameters;

            /** The result of the Biprimality test. True if succeed, false if not.*/
            public bool passes;

            public BiprimalityTestResult(BigInteger N,
                BgwPrivateParams bgwPrivateParameters, bool passes)
            {
                this.N = N;
                this.passes = passes;
                BgwPrivateParameters = bgwPrivateParameters;
            }
        }
  
    }
}