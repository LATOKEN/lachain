using Phorkus.Proto;

namespace Phorkus.Core.Consensus
{
    public interface IConsensusManager
    {
        void AdvanceEra(long newEra);
        void Dispatch(ConsensusMessage message, int from);
        void Start(long startingEra);
        void Terminate();
    }
}