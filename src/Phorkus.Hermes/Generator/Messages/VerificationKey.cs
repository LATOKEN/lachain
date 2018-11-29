using Org.BouncyCastle.Math;

namespace Phorkus.Hermes.Generator.Messages
{
    public class VerificationKey
    {
        /** A verification key*/
        public BigInteger verificationKey;

        public VerificationKey(BigInteger verificationKey)
        {
            this.verificationKey = verificationKey;
        }
    }
}