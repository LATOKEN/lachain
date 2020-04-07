using System.Collections.Generic;
using System.Linq;
using Lachain.Crypto.MCL.BLS12_381;

namespace Lachain.Consensus.TPKE.Data
{
    public class Commitment
    {
        private readonly G1[] _coefficients;
        private readonly int _degree;

        internal Commitment(int degree, IEnumerable<G1> c)
        {
            _coefficients = c.ToArray();
            _degree = degree;
        }

        public G1 Evaluate(int x, int y)
        {
            var result = G1.Zero;
            var powX = Mcl.Powers(Fr.FromInt(x), _degree + 1);
            var powY = Mcl.Powers(Fr.FromInt(y), _degree + 1);
            for (var i = 0; i <= _degree; ++i)
            {
                for (var j = 0; j <= _degree; ++j)
                {
                    result += _coefficients[Index(i, j)] * powX[i] * powY[j];
                }
            }

            return result;
        }

        public IEnumerable<G1> Evaluate(int x)
        {
            var row = Enumerable.Range(0, _degree + 1).Select(_ => G1.Zero).ToArray();
            for (var i = 0; i <= _degree; ++i)
            {
                var xPowJ = Fr.One;
                var frX = Fr.FromInt(x);
                for (var j = 0; j <= _degree; ++j)
                {
                    row[i] += _coefficients[Index(i, j)] * xPowJ;
                    xPowJ *= frX;
                }
            }

            return row;
        }

        private int Index(int i, int j)
        {
            return i * (i + 1) / 2 + j;
        }
    }
}