using Org.BouncyCastle.Math;

namespace Phorkus.Party.Generator.State
{
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
}