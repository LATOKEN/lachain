using System;
using System.Collections.Generic;
using System.Linq;
using Phorkus.Crypto.MCL.BLS12_381;

namespace Phorkus.Consensus.CommonCoin.ThresholdCrypto
{
    public class PublicKeySet
    {
        private readonly IDictionary<PublicKey, int> _keyIndex;
        public PublicKey SharedPublicKey { get; }

        public PublicKeySet()
        {
            // TODO: ctor
            _keyIndex = new Dictionary<PublicKey, int>();
            SharedPublicKey = new PublicKey(G1.Generator);
            Threshold = 0;
        }

        public int Count => _keyIndex.Count;
        public int Threshold { get; }

        public int GetIndex(PublicKey key)
        {
            if (!_keyIndex.TryGetValue(key, out var idx)) return -1;
            return idx;
        }

        public Signature AssembleSignature(IReadOnlyCollection<KeyValuePair<int, SignatureShare>> shares)
        {
            var xs = shares.Take(Threshold + 1).Select(pair => Fr.FromInt(pair.Key + 1)).ToArray();
            var ys = shares.Take(Threshold + 1).Select(pair => pair.Value.RawSignature).ToArray();
            if (xs.Length <= Threshold || ys.Length <= Threshold)
                throw new ArgumentException("not enough shares for signature");
            return new Signature(Mcl.LagrangeInterpolate(xs, ys));
        }
    }
}