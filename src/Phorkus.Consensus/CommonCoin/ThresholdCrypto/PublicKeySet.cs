using System;
using System.Collections.Generic;
using System.Linq;
using Phorkus.Crypto.MCL.BLS12_381;

namespace Phorkus.Consensus.CommonCoin.ThresholdCrypto
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
            SharedPublicKey =
                new PublicKey(
                    _keys.Aggregate((res, keyShare) => new PublicKeyShare(res.RawKey + keyShare.RawKey)).RawKey
                );
            Threshold = faulty;
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
            return new Signature(Mcl.LagrangeInterpolate(xs, ys));
        }
    }
}