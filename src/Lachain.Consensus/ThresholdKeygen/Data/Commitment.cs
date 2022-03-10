using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using Lachain.Utility.Utils;
using MCL.BLS12_381.Net;

namespace Lachain.Consensus.ThresholdKeygen.Data
{
    [DataContract]
    public class Commitment : IEquatable<Commitment>
    {
        [DataMember]
        private readonly G1[] _coefficients;
        [DataMember]
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
            var powX = MclBls12381.Powers(Fr.FromInt(x), Degree + 1);
            var powY = MclBls12381.Powers(Fr.FromInt(y), Degree + 1);
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

        private static int Index(int i, int j)
        {
            if (i > j) (i, j) = (j, i);
            return i * (i + 1) / 2 + j;
        }

        //public byte[] ToBytes()
        //{
        //    return _coefficients.Select(x => x.ToBytes()).Flatten().ToArray();
        //}

        public static Commitment FromBytes(IEnumerable<byte> buffer)
        {
            return new Commitment(buffer.Batch(G1.ByteSize)
                .Select(x => x.ToArray())
                .Select(b => G1.FromBytes(b.ToArray()))
            );
        }

        public bool IsValid()
        {
            return _coefficients.Length == (Degree + 1) * (Degree + 2) / 2 &&
                   _coefficients.All(x => x.IsValid());
        }

        public bool Equals(Commitment? other)
        {
            if (other is null) return false;
            if (ReferenceEquals(this, other)) return true;
            return _coefficients.SequenceEqual(other._coefficients);
        }

        public override bool Equals(object? obj)
        {
            if (obj is null) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((Commitment) obj);
        }

        public override int GetHashCode()
        {
            return _coefficients.GetHashCode();
        }

        public byte[] ToBytes()
        {
            using var ms = new MemoryStream();
            var serializer = new DataContractSerializer(typeof(Commitment));
            serializer.WriteObject(ms, this);
            return ms.ToArray();
        }

        public static Commitment? FromBytes(ReadOnlyMemory<byte> bytes)
        {
            if(bytes.ToArray() == null)
            {
                return default;
            }
            using var memStream = new MemoryStream(bytes.ToArray());
            var serializer = new DataContractSerializer(typeof(Commitment));
            var obj = (Commitment?)serializer.ReadObject(memStream);
            return obj;
        }
    }
}