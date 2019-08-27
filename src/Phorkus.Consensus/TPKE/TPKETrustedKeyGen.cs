using System;
using System.Collections.Generic;
using System.Linq;
using Phorkus.Crypto.MCL.BLS12_381;
using Phorkus.Utility.Utils;

namespace Phorkus.Consensus.TPKE
{
    public class TPKETrustedKeyGen
    {
        private readonly Fr[] _coeffs;
        private readonly int _degree;
        private readonly int _parties;

        public TPKETrustedKeyGen(int n, int f)
        {
            if (n <= 3 * f) throw new ArgumentException($"n should be greater than 3*f, but {n} <= 3 * {f} = {3 * f}");
            _degree = f;
            _parties = n;
            _coeffs = new Fr[_degree];

            for (var i = 0; i < _degree; ++i)
                _coeffs[i] = Fr.GetRandom();
        }

        public TPKEPubKey GetPubKey()
        {
            return new TPKEPubKey(G1.Generator * Mcl.GetValue(_coeffs.AsDynamic(), 0, Fr.Zero), _degree);
        }

        public TPKEPrivKey GetPrivKey(int i)
        {
            return new TPKEPrivKey(Mcl.GetValue(_coeffs.AsDynamic(), i + 1, Fr.Zero), i);
        }

        public TPKEVerificationKey GetVerificationKey()
        {
            var zs = new List<G2>();
            for (var i = 0; i < _parties; ++i)
            {
                zs.Add(G2.Generator * Mcl.GetValue(_coeffs.AsDynamic(), i + 1, Fr.Zero));
            }
            return new TPKEVerificationKey(G1.Generator * Mcl.GetValue(_coeffs.AsDynamic(), 0, Fr.Zero), _degree, zs.ToArray());
        }
    }
}