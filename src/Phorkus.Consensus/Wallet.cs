using System.Collections.Generic;
using Phorkus.Consensus.CommonCoin.ThresholdSignature;
using Phorkus.Consensus.TPKE;
using Phorkus.Utility.Utils;

namespace Phorkus.Consensus
{
    public class Wallet : IWallet
    {
        public int N { get; }
        public int F { get; }
        public TPKEPubKey TpkePubKey { get; set; }
        public TPKEPrivKey TpkePrivKey { get; set; }
        public TPKEVerificationKey TpkeVerificationKey { get; set; }

        public PublicKeySet PublicKeySet { get; set; }
        public PrivateKeyShare PrivateKeyShare { get; set;  }
        public ISet<IProtocolIdentifier> ProtocolIds { get; } = new HashSet<IProtocolIdentifier>();

        public Wallet(int n, int f)
        {
            N = n;
            F = f;
        }
    }
}