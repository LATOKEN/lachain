using Phorkus.Consensus;
using Phorkus.Proto;

namespace Phorkus.ConsensusTest
{
    public static class TestUtils
    {
        public static IWallet EmptyWallet(int n, int f)
        {
            return new Wallet(n, f,
                null, null, null,
                null, null,
                null, null, new ECDSAPublicKey[] { }
            );
        }
    }
}