using System.Collections.Generic;
using System.Linq;
using Lachain.Crypto.ThresholdSignature;
using Lachain.Proto;
using PublicKey = Lachain.Crypto.TPKE.PublicKey;

namespace Lachain.Consensus
{
    public class PublicConsensusKeySet : IPublicConsensusKeySet
    {
        public int N { get; }
        public int F { get; }
        public PublicKey TpkePublicKey { get; }
        public PublicKeySet ThresholdSignaturePublicKeySet { get; }
        private readonly List<ECDSAPublicKey> _ecdsaPublicKeys;
        public IList<ECDSAPublicKey> EcdsaPublicKeySet => _ecdsaPublicKeys;

        public PublicConsensusKeySet(int n, int f,
            PublicKey tpkePublicKey,
            PublicKeySet thresholdSignaturePublicKeySet,
            IEnumerable<ECDSAPublicKey> ecdsaPublicKeys
        )
        {
            N = n;
            F = f;
            TpkePublicKey = tpkePublicKey;
            ThresholdSignaturePublicKeySet = thresholdSignaturePublicKeySet;
            _ecdsaPublicKeys = ecdsaPublicKeys.ToList();
        }
    }
}