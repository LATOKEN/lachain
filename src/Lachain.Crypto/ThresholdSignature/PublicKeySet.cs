using System;
using System.Collections.Generic;
using System.Linq;
using Lachain.Crypto.MCL.BLS12_381;

namespace Lachain.Crypto.ThresholdSignature
{
    public class PublicKeySet
    {
        private readonly IDictionary<PublicKeyShare, int> _keyIndex;
        public PublicKey SharedPublicKey { get; }
        private readonly PublicKeyShare[] _keys;

        public PublicKeySet(IEnumerable<PublicKeyShare> pubKeyShares, int faulty)
        {
            // TODO: ctor
            _keys = pubKeyShares.ToArray();
            _keyIndex = _keys.Select((share, i) => (share, i)).ToDictionary(t => t.Item1, t => t.Item2);
            SharedPublicKey = new PublicKey(AssemblePublicKey(_keys.Select(share => share.RawKey), _keys.Length));
            Threshold = faulty;
        }

        private static G1 AssemblePublicKey(IEnumerable<G1> shares, int n)
        {
            var xs = Enumerable.Range(1, n).Select(Fr.FromInt).ToArray();
            var ys = shares.ToArray();
            return Mcl.LagrangeInterpolateG1(xs, ys);
        }

        public int Count => _keyIndex.Count;
        public int Threshold { get; }

        public int GetIndex(PublicKeyShare key)
        {
            if (!_keyIndex.TryGetValue(key, out var idx)) return -1;
            return idx;
        }

        public PublicKeyShare this[int idx] => _keys[idx];

        public Signature AssembleSignature(IEnumerable<KeyValuePair<int, SignatureShare>> shares)
        {
            var keyValuePairs = shares as KeyValuePair<int, SignatureShare>[] ?? shares.ToArray();
            var xs = keyValuePairs.Take(Threshold + 1).Select(pair => Fr.FromInt(pair.Key + 1)).ToArray();
            var ys = keyValuePairs.Take(Threshold + 1).Select(pair => pair.Value.RawSignature).ToArray();
            if (xs.Length <= Threshold || ys.Length <= Threshold)
                throw new ArgumentException("not enough shares for signature");
            return new Signature(Mcl.LagrangeInterpolateG2(xs, ys));
        }
    }
}