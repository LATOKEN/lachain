using Lachain.Consensus;
using Lachain.Consensus.Messages;

namespace Lachain.ConsensusTest
{
    public class SilentProtocol<TId> : IConsensusProtocol where TId : IProtocolIdentifier
    {
        public SilentProtocol(TId id)
        {
            Id = id;
        }
        
        public IProtocolIdentifier Id { get; }
        public void ReceiveMessage(MessageEnvelope message)
        {
        }

        public void Start()
        {
        }

        public void WaitFinish()
        {
        }

        public void WaitResult()
        {
            throw new System.NotImplementedException();
        }

        public void Terminate()
        {
            throw new System.NotImplementedException();
        }

        public bool Terminated => true;
    }
}