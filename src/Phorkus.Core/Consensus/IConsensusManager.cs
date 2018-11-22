using Phorkus.Proto;

namespace Phorkus.Core.Consensus
{
    public interface IConsensusManager
    {
        void Start();
        void Stop();
        void HandleConsensusMessage(ConsensusMessage message);
    }
}