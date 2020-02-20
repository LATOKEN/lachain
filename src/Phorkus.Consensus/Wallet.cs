using System.Collections.Generic;
using System.Linq;
using Phorkus.Crypto.ThresholdSignature;
using Phorkus.Crypto.TPKE;
using Phorkus.Proto;
using PublicKey = Phorkus.Crypto.TPKE.PublicKey;

namespace Phorkus.Consensus
{
    public class Wallet : IWallet
    {
        public int N { get; }
        public int F { get; }
        public PublicKey TpkePublicKey { get; }
        public PrivateKey TpkePrivateKey { get; }
        public VerificationKey TpkeVerificationKey { get; }

        public PublicKeySet ThresholdSignaturePublicKeySet { get; }
        public PrivateKeyShare ThresholdSignaturePrivateKeyShare { get; }

        public ECDSAPublicKey EcdsaPublicKey { get; }

        public ECDSAPrivateKey EcdsaPrivateKey { get; }

        private readonly List<ECDSAPublicKey> _ecdsaPublicKeys;
        public IEnumerable<ECDSAPublicKey> EcdsaPublicKeySet => _ecdsaPublicKeys;
        public ISet<IProtocolIdentifier> ProtocolIds { get; } = new HashSet<IProtocolIdentifier>(); // TODO: delete this

        public Wallet(int n, int f,
            PublicKey tpkePublicKey, PrivateKey tpkePrivateKey, VerificationKey tpkeVerificationKey,
            PublicKeySet thresholdSignaturePublicKeySet, PrivateKeyShare thresholdSignaturePrivateKeyShare,
            ECDSAPublicKey ecdsaPublicKey, ECDSAPrivateKey ecdsaPrivateKey, IEnumerable<ECDSAPublicKey> ecdsaPublicKeys
        )
        {
            N = n;
            F = f;
            TpkePrivateKey = tpkePrivateKey;
            TpkePublicKey = tpkePublicKey;
            TpkeVerificationKey = tpkeVerificationKey;
            ThresholdSignaturePrivateKeyShare = thresholdSignaturePrivateKeyShare;
            ThresholdSignaturePublicKeySet = thresholdSignaturePublicKeySet;
            EcdsaPublicKey = ecdsaPublicKey;
            EcdsaPrivateKey = ecdsaPrivateKey;
            _ecdsaPublicKeys = ecdsaPublicKeys.ToList();
        }
    }
}