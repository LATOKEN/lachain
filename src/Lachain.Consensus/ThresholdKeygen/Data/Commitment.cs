using System;
using System.Collections.Generic;
using System.Linq;
using Lachain.Crypto.MCL.BLS12_381;
using Lachain.Utility.Utils;

namespace Lachain.Consensus.ThresholdKeygen.Data
{
    public class Commitment
    {
        private readonly G1[] _coefficients;
        public readonly int Degree;

        internal Commitment(IEnumerable<G1> g1)
        {
            _coefficients = g1.ToArray();
            Degree = 0;
            while ((Degree + 1) * (Degree + 2) / 2 < _coefficients.Length) ++Degree;
            if ((Degree + 1) * (Degree + 2) / 2 != _coefficients.Length)
                throw new ArgumentException("Invalid length of coefficient vector");
        }

        public G1 Evaluate(int x, int y)
        {
            var result = G1.Zero;
            var powX = Mcl.Powers(Fr.FromInt(x), Degree + 1);
            var powY = Mcl.Powers(Fr.FromInt(y), Degree + 1);
            for (var i = 0; i <= Degree; ++i)
            {
                for (var j = 0; j <= Degree; ++j)
                {
                    result += _coefficients[Index(i, j)] * powX[i] * powY[j];
                }
            }

            return result;
        }

        public IEnumerable<G1> Evaluate(int x)
        {
            var row = Enumerable.Range(0, Degree + 1).Select(_ => G1.Zero).ToArray();
            var frX = Fr.FromInt(x);
            for (var i = 0; i <= Degree; ++i)
            {
                var xPowJ = Fr.One;
                for (var j = 0; j <= Degree; ++j)
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

        public byte[] ToBytes()
        {
            return _coefficients.Select(G1.ToBytes)
                .Cast<IEnumerable<byte>>()
                .Aggregate((a, b) => a.Concat(b))
                .ToArray();
        }

        public static Commitment FromBytes(IEnumerable<byte> buffer)
        {
            return new Commitment(buffer.Batch(G1.ByteSize)
                .Select(x => x.ToArray())
                .Select(G1.FromBytes));
        }
    }
}