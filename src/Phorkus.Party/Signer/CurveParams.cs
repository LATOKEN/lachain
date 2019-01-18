using Org.BouncyCastle.Asn1.Sec;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.Math.EC;

namespace Phorkus.Party.Signer
{
    public class CurveParams
    {
        public ECDomainParameters Curve { get; }
        public ECPoint G { get; }
        public BigInteger Q { get; }

        public string Name { get; }

        public CurveParams(string curveName)
        {
            var parameters = SecNamedCurves.GetByName(curveName);
            if (parameters is null)
                throw new InvalidCurveException("Unable to resolve curve by name (" + curveName + ")");
            Curve = new ECDomainParameters(parameters.Curve, parameters.G, parameters.N, parameters.H);
            G = Curve.G;
            Name = curveName;
            Q = parameters.N;
        }
    }
}