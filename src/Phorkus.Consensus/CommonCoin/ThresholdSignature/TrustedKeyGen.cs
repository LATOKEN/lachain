using System;
using System.Collections.Generic;
using System.Linq;
using Phorkus.Crypto.MCL.BLS12_381;

namespace Phorkus.Consensus.CommonCoin.ThresholdSignature
{
    public class TrustedKeyGen
    {
        private readonly Fr[] _coeffs;
        private readonly int _degree;
        private readonly int _parties;

        public TrustedKeyGen(int n, int f, Random rng)
        {
            if (n <= 3 * f) throw new ArgumentException("n should be greater than 3*f");
            _degree = f;
            _parties = n;
            _coeffs = new Fr[_degree + 1];
            
            for (var i = 0; i <= f; ++i)
                _coeffs[i] = Fr.FromInt(rng.Next());
        }
        
        public IEnumerable<PrivateKeyShare> GetPrivateShares()
        {
            var shares = new Fr[_parties];
            for (var i = 0; i < _parties; ++i)
                shares[i] = EvaluatePoly(Fr.FromInt(i + 1));
            return shares.Select(share => new PrivateKeyShare(share));
        }

        private Fr EvaluatePoly(Fr x)
        {
            var result = Fr.FromInt(0);
            var curPower = Fr.FromInt(1);
            for (var i = 0; i < _degree; ++i)
            {
                var s = _coeffs[i];
                s.Mul(s, curPower);
                result.Add(result, s);
                curPower.Mul(curPower, x);
            }

            return result;
        }
    }
}