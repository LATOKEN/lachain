using Phorkus.Proto;

namespace Phorkus.Core.Consensus
{
    public interface IConsensusManager
    {
        void AdvanceEra(long newEra);
        void Dispatch(ConsensusMessage message);
        void Start(long startingEra);
        void Terminate();
    }
}