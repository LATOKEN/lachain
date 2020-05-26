using Lachain.Consensus;

namespace Lachain.ConsensusTest
{
    public static class TestUtils
    {
        public static IPrivateConsensusKeySet EmptyWallet(int n, int f)
        {
            return new PrivateConsensusKeySet(null, null, null);
        }
    }
}