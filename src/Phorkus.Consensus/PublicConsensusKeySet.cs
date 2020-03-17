using System.Collections.Generic;
using System.Linq;
using Phorkus.Crypto.ThresholdSignature;
using Phorkus.Crypto.TPKE;
using Phorkus.Proto;
using PublicKey = Phorkus.Crypto.TPKE.PublicKey;

namespace Phorkus.Consensus
{
    public class PublicConsensusKeySet : IPublicConsensusKeySet
    {
        public int N { get; }
        public int F { get; }
        public PublicKey TpkePublicKey { get; }
        public VerificationKey TpkeVerificationKey { get; }

        public PublicKeySet ThresholdSignaturePublicKeySet { get; }
        private readonly List<ECDSAPublicKey> _ecdsaPublicKeys;
        public IList<ECDSAPublicKey> EcdsaPublicKeySet => _ecdsaPublicKeys;

        public PublicConsensusKeySet(int n, int f,
            PublicKey tpkePublicKey, VerificationKey tpkeVerificationKey,
            PublicKeySet thresholdSignaturePublicKeySet,
            IEnumerable<ECDSAPublicKey> ecdsaPublicKeys
        )
        {
            N = n;
            F = f;
            TpkePublicKey = tpkePublicKey;
            TpkeVerificationKey = tpkeVerificationKey;
            ThresholdSignaturePublicKeySet = thresholdSignaturePublicKeySet;
            _ecdsaPublicKeys = ecdsaPublicKeys.ToList();
        }
    }
}