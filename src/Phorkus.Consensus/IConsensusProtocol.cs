using Phorkus.Consensus.Messages;

namespace Phorkus.Consensus
{
    public interface IConsensusProtocol
    {
        IProtocolIdentifier Id { get; }
        void ReceiveMessage(MessageEnvelope message);

        void Start();
        void WaitFinish();
        void WaitResult();

        void Terminate();

        bool Terminated { get; }
    }
}