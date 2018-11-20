using Phorkus.Core.Proto;

namespace Phorkus.Core.Blockchain.Consensus
{
    public interface IConsensusManager
    {
        void Start();
        void Stop();
        void HandleConsensusMessage(ConsensusMessage message);
    }
}