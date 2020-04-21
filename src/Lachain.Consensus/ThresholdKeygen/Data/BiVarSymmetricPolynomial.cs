using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Lachain.Crypto.MCL.BLS12_381;

namespace Lachain.Consensus.ThresholdKeygen.Data
{
    public class BiVarSymmetricPolynomial
    {
        private readonly Fr[] _coefficients;

        private readonly int _degree;

        private BiVarSymmetricPolynomial(int degree, IEnumerable<Fr> c)
        {
            _coefficients = c.ToArray();
            _degree = degree;
            Debug.Assert(Index(_degree, _degree) + 1 == (_degree + 2) * (_degree + 1) / 2);
            if (_coefficients.Length != (_degree + 2) * (_degree + 1) / 2)
                throw new ArgumentException("Wrong number of coefficients");
        }

        public static BiVarSymmetricPolynomial Random(int degree)
        {
            return new BiVarSymmetricPolynomial(
                degree,
                Enumerable.Range(0, degree + 1)
                    .SelectMany(i => Enumerable.Range(0, i + 1).Select(j => Fr.GetRandom()))
            );
        }

        public Commitment Commit()
        {
            return new Commitment(_coefficients.Select(x => G1.Generator * x));
        }

        public IEnumerable<Fr> Evaluate(int x)
        {
            var row = Enumerable.Range(0, _degree + 1).Select(_ => Fr.Zero).ToArray();
            var frX = Fr.FromInt(x);
            for (var i = 0; i <= _degree; ++i)
            {
                var xPowJ = Fr.One;
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
            if (i > j) (i, j) = (j, i);
            return i * (i + 1) / 2 + j;
        }
    }
}