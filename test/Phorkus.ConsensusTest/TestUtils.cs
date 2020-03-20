using Phorkus.Consensus;

namespace Phorkus.ConsensusTest
{
    public static class TestUtils
    {
        public static IPrivateConsensusKeySet EmptyWallet(int n, int f)
        {
            return new PrivateConsensusKeySet(null, null, null);
        }
    }
}