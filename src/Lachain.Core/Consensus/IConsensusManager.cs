using Lachain.Proto;

namespace Lachain.Core.Consensus
{
    public interface IConsensusManager
    {
        void AdvanceEra(long newEra);
        void Dispatch(ConsensusMessage message, ECDSAPublicKey publicKey);
        void Start(long startingEra);
        void Terminate();
    }
}