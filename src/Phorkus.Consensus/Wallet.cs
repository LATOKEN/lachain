using Phorkus.Consensus.CommonCoin.ThresholdSignature;
using Phorkus.Utility.Utils;

namespace Phorkus.Consensus
{
    public class Wallet : IWallet
    {
        public int N { get; }
        public int F { get; }

        public PublicKeySet PublicKeySet { get; set; }
        public PrivateKeyShare PrivateKeyShare { get; set;  }

        public Wallet(int n, int f)
        {
            N = n;
            F = f;
        }
    }
}