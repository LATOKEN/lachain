using Phorkus.Proto;

namespace Phorkus.Consensus
{
    public interface IMessageDispatcher
    {
        IConsensusProtocol RegisterAlgorithm(IConsensusProtocol algo, IProtocolIdentifier id);
        void DispatchMessage(ConsensusMessage message);
    }
}