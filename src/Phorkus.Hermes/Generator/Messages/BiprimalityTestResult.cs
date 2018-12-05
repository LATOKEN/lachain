using Org.BouncyCastle.Math;

namespace Phorkus.Hermes.Generator.Messages
{
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