using Phorkus.Core.Proto;

namespace Phorkus.Core.Blockchain.Consensus
{
    public interface IConsensusManager
    {
        void Start();
        void HandleConsensusMessage(ConsensusMessage message);
    }
}