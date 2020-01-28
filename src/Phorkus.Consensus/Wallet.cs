using System.Collections.Generic;
using Phorkus.Crypto.ThresholdSignature;
using Phorkus.Crypto.TPKE;
using PublicKey = Phorkus.Crypto.TPKE.PublicKey;

namespace Phorkus.Consensus
{
    public class Wallet : IWallet
    {
        public int N { get; }
        public int F { get; }
        public PublicKey TpkePublicKey { get; set; }
        public PrivateKey TpkePrivateKey { get; set; }
        public VerificationKey TpkeVerificationKey { get; set; }

        public PublicKeySet ThresholdSignaturePublicKeySet { get; set; }
        public PrivateKeyShare ThresholdSignaturePrivateKeyShare { get; set;  }
        public ISet<IProtocolIdentifier> ProtocolIds { get; } = new HashSet<IProtocolIdentifier>(); // TODO: delete this

        public Wallet(int n, int f)
        {
            N = n;
            F = f;
        }
    }
}