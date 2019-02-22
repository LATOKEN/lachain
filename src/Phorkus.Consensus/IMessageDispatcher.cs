using Phorkus.Proto;

namespace Phorkus.Consensus
{
    public interface IMessageDispatcher
    {
        IConsensusProtocol RegistgerAlgorithm(IConsensusProtocol algo, IProtocolIdentifier id);
        void DispatchMessage(ConsensusMessage message);
    }
}