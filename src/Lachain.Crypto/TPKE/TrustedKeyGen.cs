using System;
using MCL.BLS12_381.Net;

namespace Lachain.Crypto.TPKE
{
    public class TrustedKeyGen
    {
        private readonly Fr[] _coeffs;
        private readonly int _degree;

        public TrustedKeyGen(int n, int f)
        {
            if (n <= 3 * f) throw new ArgumentException($"n should be greater than 3*f, but {n} <= 3 * {f} = {3 * f}");
            _degree = f;
            _coeffs = new Fr[_degree];

            for (var i = 0; i < _degree; ++i)
                _coeffs[i] = Fr.GetRandom();
        }

        public PublicKey GetPubKey()
        {
            return new PublicKey(G1.Generator * MclBls12381.EvaluatePolynomial(_coeffs, Fr.FromInt(0)), _degree);
        }

        public PrivateKey GetPrivKey(int i)
        {
            return new PrivateKey(MclBls12381.EvaluatePolynomial(_coeffs, Fr.FromInt(i + 1)), i);
        }
    }
}