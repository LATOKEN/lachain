using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.Math.EC;
using Phorkus.Hermes.Crypto.Key;

namespace Phorkus.Hermes.Signer
{
    public class PublicParameters
    {
        public byte[] gRaw;
        public BigInteger h1;
        public BigInteger h2;
        public BigInteger nTilde;
        public PaillierKey paillierPubKey;

        public PublicParameters(ECDomainParameters CURVE, BigInteger nTilde, BigInteger h1, BigInteger h2,
            PaillierKey paillierPubKey)
        {
            gRaw = CURVE.G.GetEncoded();
            this.nTilde = nTilde;
            this.h1 = h1;
            this.h2 = h2;
            this.paillierPubKey = paillierPubKey;
        }

        public ECPoint getG(ECDomainParameters curve)
        {
            return curve.Curve.DecodePoint(gRaw);
        }
    }
}