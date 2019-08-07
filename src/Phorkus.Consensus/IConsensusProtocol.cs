using Phorkus.Consensus.Messages;

namespace Phorkus.Consensus
{
    public interface IConsensusProtocol
    {
        IProtocolIdentifier Id { get; }
        void ReceiveMessage(MessageEnvelope message);

        void Start();

        bool Terminated { get; }
    }
}