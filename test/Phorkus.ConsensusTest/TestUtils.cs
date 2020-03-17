using Phorkus.Consensus;
using Phorkus.Proto;

namespace Phorkus.ConsensusTest
{
    public static class TestUtils
    {
        public static IPrivateConsensusKeySet EmptyWallet(int n, int f)
        {
            return new PublicConsensusKeySet(n, f,
                null, null, null,
                null, null,
                null, null, new ECDSAPublicKey[] { }
            );
        }
    }
}