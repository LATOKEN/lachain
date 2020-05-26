using System;
using System.IO;
using System.Linq;
using Lachain.Crypto.MCL.BLS12_381;
using Lachain.Utility.Serialization;
using Lachain.Utility.Utils;

namespace Lachain.Consensus.ThresholdKeygen.Data
{
    public class State : IEquatable<State>
    {
        public Commitment? Commitment { get; set; }
        public readonly Fr[] Values;
        public readonly bool[] Acks;

        public State(int n)
        {
            Values = new Fr[n];
            Acks = new bool[n];
        }

        public int ValueCount()
        {
            return Acks.Select(x => x ? 1 : 0).Sum();
        }

        public Fr InterpolateValues()
        {
            if (Commitment is null) throw new ArgumentException("Cannot interpolate without commitment");
            var xs = Acks.WithIndex()
                .Where(x => x.item)
                .Select(x => x.index + 1)
                .Select(Fr.FromInt)
                .Take(Commitment.Degree + 1)
                .ToArray();
            var ys = Acks.WithIndex()
                .Where(x => x.item)
                .Select(x => Values[x.index])
                .Take(Commitment.Degree + 1)
                .ToArray();
            if (xs.Length != Commitment.Degree + 1 || ys.Length != Commitment.Degree + 1)
                throw new Exception("Cannot interpolate values");
            return Mcl.LagrangeInterpolateFr(xs, ys);
        }

        public byte[] ToBytes()
        {
            var commitment = Commitment?.ToBytes() ?? new byte[] { };
            var values = Values.Select(x => x.ToBytes()).Flatten().ToArray();
            var acks = Acks.Select(x => x ? (byte) 1 : (byte) 0).ToArray();
            using var stream = new MemoryStream();
            stream.Write(commitment.Length.ToBytes().ToArray());
            stream.Write(commitment);
            stream.Write(Acks.Length.ToBytes().ToArray());
            stream.Write(values);
            stream.Write(acks);
            return stream.ToArray();
        }

        public static State FromBytes(ReadOnlyMemory<byte> bytes)
        {
            var cLen = bytes.Slice(0, 4).Span.ToInt32();
            var commitment = cLen != 0 ? Commitment.FromBytes(bytes.Slice(4, cLen).ToArray()) : null;
            var n = bytes.Slice(4 + cLen, 4).Span.ToInt32();
            var values = bytes.Slice(8 + cLen, n * Fr.ByteSize).ToArray()
                .Batch(Fr.ByteSize)
                .Select(x => Fr.FromBytes(x.ToArray()))
                .ToArray();
            var acks = bytes.Slice(8 + cLen + n * Fr.ByteSize).ToArray()
                .Select(b => b != 0)
                .ToArray();
            var result = new State(values.Length) {Commitment = commitment};
            for (var i = 0; i < n; ++i)
            {
                result.Values[i] = values[i];
                result.Acks[i] = acks[i];
            }

            return result;
        }

        public bool Equals(State? other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return Values.SequenceEqual(other.Values) &&
                   Acks.SequenceEqual(other.Acks) &&
                   Equals(Commitment, other.Commitment);
        }

        public override bool Equals(object? obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((State) obj);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Values, Acks, Commitment);
        }
    }
}