using System;
using System.Collections.Generic;
using System.Linq;
using Phorkus.Crypto.MCL.BLS12_381;
using Phorkus.Utility.Utils;

namespace Phorkus.Crypto.ThresholdSignature
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
            _coeffs = new Fr[_degree + 1];

            for (var i = 0; i <= f; ++i)
                _coeffs[i] = Fr.GetRandom();
        }
        
        public IEnumerable<PrivateKeyShare> GetPrivateShares()
        {
            var shares = new Fr[_parties];
            for (var i = 0; i < _parties; ++i)
                shares[i] = Mcl.GetValue(_coeffs.AsDynamic(), i + 1, Fr.Zero);
            return shares.Select(share => new PrivateKeyShare(share));
        }
    }
}