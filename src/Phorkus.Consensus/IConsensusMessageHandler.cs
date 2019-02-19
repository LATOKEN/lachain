using Phorkus.Proto;

namespace Phorkus.Consensus
{
    public interface IConsensusMessageHandler
    {
        void HandleMessage(ConsensusMessage message);
    }
}