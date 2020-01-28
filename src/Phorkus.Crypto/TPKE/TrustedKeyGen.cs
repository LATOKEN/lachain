using System;
using System.Collections.Generic;
using Phorkus.Crypto.MCL.BLS12_381;
using Phorkus.Utility.Utils;

namespace Phorkus.Crypto.TPKE
{
    public class TrustedKeyGen
    {
        private readonly Fr[] _coeffs;
        private readonly int _degree;
        private readonly int _parties;

        public TrustedKeyGen(int n, int f)
        {
            if (n <= 3 * f) throw new ArgumentException($"n should be greater than 3*f, but {n} <= 3 * {f} = {3 * f}");
            _degree = f;
            _parties = n;
            _coeffs = new Fr[_degree];

            for (var i = 0; i < _degree; ++i)
                _coeffs[i] = Fr.GetRandom();
        }

        public PublicKey GetPubKey()
        {
            return new PublicKey(G1.Generator * Mcl.GetValue(_coeffs.AsDynamic(), 0, Fr.Zero), _degree);
        }

        public PrivateKey GetPrivKey(int i)
        {
            return new PrivateKey(Mcl.GetValue(_coeffs.AsDynamic(), i + 1, Fr.Zero), i);
        }

        public VerificationKey GetVerificationKey()
        {
            var zs = new List<G2>();
            for (var i = 0; i < _parties; ++i)
            {
                zs.Add(G2.Generator * Mcl.GetValue(_coeffs.AsDynamic(), i + 1, Fr.Zero));
            }
            return new VerificationKey(G1.Generator * Mcl.GetValue(_coeffs.AsDynamic(), 0, Fr.Zero), _degree, zs.ToArray());
        }
    }
}