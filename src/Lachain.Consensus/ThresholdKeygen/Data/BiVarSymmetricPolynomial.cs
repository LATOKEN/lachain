using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using MCL.BLS12_381.Net;

namespace Lachain.Consensus.ThresholdKeygen.Data
{
    [DataContract]
    public class BiVarSymmetricPolynomial
    {
        [DataMember]
        private readonly Fr[] _coefficients;
        [DataMember]
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

        private static int Index(int i, int j)
        {
            if (i > j) (i, j) = (j, i);
            return i * (i + 1) / 2 + j;
        }

        public byte[] ToBytes()
        {
            using var ms = new MemoryStream();
            var serializer = new DataContractSerializer(typeof(BiVarSymmetricPolynomial));
            serializer.WriteObject(ms, this);

            return ms.ToArray();
        }

        public static BiVarSymmetricPolynomial? FromBytes(ReadOnlyMemory<byte> bytes)
        {
            if(bytes.ToArray() == null)
            {
                return default;
            }

            using var memStream = new MemoryStream(bytes.ToArray());
            var serializer = new DataContractSerializer(typeof(BiVarSymmetricPolynomial));
            var obj = (BiVarSymmetricPolynomial?)serializer.ReadObject(memStream);

            return obj;
        }
    }
}