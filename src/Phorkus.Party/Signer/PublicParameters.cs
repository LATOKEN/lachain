using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.Math.EC;
using Phorkus.Party.Crypto.Key;

namespace Phorkus.Party.Signer
{
    public class PublicParameters
    {
        public byte[] gRaw;
        public ECDomainParameters domainParameters;
        public BigInteger h1;
        public BigInteger h2;
        public BigInteger nTilde;
        public PaillierKey paillierPubKey;

        public PublicParameters(ECDomainParameters curve, BigInteger nTilde, BigInteger h1, BigInteger h2,
            PaillierKey paillierPubKey)
        {
            this.domainParameters = curve;
            gRaw = curve.G.GetEncoded();
            this.nTilde = nTilde;
            this.h1 = h1;
            this.h2 = h2;
            this.paillierPubKey = paillierPubKey;
        }
        
        public ECPoint getG()
        {
            return domainParameters.Curve.DecodePoint(gRaw);
        }
    }
}