using Phorkus.Proto;

namespace Phorkus.Consensus
{
    public interface IMessageDispatcher
    {
        IConsensusMessageHandler RegistgerAlgorithm(IConsensusMessageHandler algo, IProtocolIdentifier id);
        void DispatchMessage(ConsensusMessage message);
    }
}