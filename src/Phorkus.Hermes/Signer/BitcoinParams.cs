using Org.BouncyCastle.Asn1.Sec;
using Org.BouncyCastle.Asn1.X9;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.Math.EC;

namespace Phorkus.Hermes.Signer
{
    public class BitcoinParams
    {
        public ECDomainParameters CURVE;
        public BigInteger q;
        public ECPoint G;

        public static BitcoinParams Instance
        {
            get
            {
                if (_cache != null)
                    return _cache;
                _cache = new BitcoinParams();
                return _cache;
            }
        }

        private static BitcoinParams _cache;

        private BitcoinParams() {
            X9ECParameters parameters = SecNamedCurves.GetByName("secp256k1");
            CURVE = new ECDomainParameters(parameters.Curve, parameters.G,
                parameters.N, parameters.H);
            q = parameters.N;
            G = CURVE.G;
        }
    }
}