using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Lachain.Crypto.MCL.BLS12_381;
using Lachain.Utility.Serialization;
using Lachain.Utility.Utils;

namespace Lachain.Crypto.ThresholdSignature
{
    public class PublicKeySet : IEquatable<PublicKeySet>
    {
        public PublicKey SharedPublicKey { get; }
        private readonly PublicKey[] _keys;
        public int Threshold { get; }

        public IReadOnlyCollection<PublicKey> Keys => _keys;
        public int Count => _keys.Length;
        public PublicKey this[int idx] => _keys[idx];

        public PublicKeySet(IEnumerable<PublicKey> pubKeyShares, int faulty)
        {
            _keys = pubKeyShares.ToArray();
            // TODO: this won't work when faulty = 0 and there are >1 players
            SharedPublicKey = new PublicKey(AssemblePublicKey(_keys.Select(share => share.RawKey), _keys.Length));
            Threshold = faulty;
        }

        private static G1 AssemblePublicKey(IEnumerable<G1> shares, int n)
        {
            var xs = Enumerable.Range(1, n).Select(Fr.FromInt).ToArray();
            var ys = shares.ToArray();
            return Mcl.LagrangeInterpolateG1(xs, ys);
        }

        public Signature AssembleSignature(IEnumerable<KeyValuePair<int, Signature>> shares)
        {
            var keyValuePairs = shares as KeyValuePair<int, Signature>[] ?? shares.ToArray();
            var xs = keyValuePairs.Take(Threshold + 1).Select(pair => Fr.FromInt(pair.Key + 1)).ToArray();
            var ys = keyValuePairs.Take(Threshold + 1).Select(pair => pair.Value.RawSignature).ToArray();
            if (xs.Length <= Threshold || ys.Length <= Threshold)
                throw new ArgumentException("not enough shares for signature");
            return new Signature(Mcl.LagrangeInterpolateG2(xs, ys));
        }

        public bool Equals(PublicKeySet? other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return _keys.SequenceEqual(other._keys);
        }

        public override bool Equals(object? obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((PublicKeySet) obj);
        }

        public override int GetHashCode()
        {
            return _keys.GetHashCode();
        }

        public IEnumerable<byte> ToBytes()
        {
            return Threshold.ToBytes().Concat(FixedWithSerializer.SerializeArray(_keys));
        }

        public static PublicKeySet FromBytes(ReadOnlyMemory<byte> bytes)
        {
            var faulty = bytes.Span.Slice(0, 4).ToInt32();
            return new PublicKeySet(FixedWithSerializer.DeserializeHomogeneous<PublicKey>(bytes.Slice(4)), faulty);
        }
    }
}