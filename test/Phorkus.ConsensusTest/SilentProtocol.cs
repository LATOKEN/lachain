using System;
using Phorkus.Consensus;
using Phorkus.Consensus.Messages;

namespace Phorkus.ConsensusTest
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
            throw new NotImplementedException();
        }

        public void Terminate()
        {
            throw new NotImplementedException();
        }

        public bool Terminated => true;
    }
}