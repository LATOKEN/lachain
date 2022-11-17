using System;
using Lachain.Consensus;
using Lachain.Consensus.Messages;
using Lachain.Proto;

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

        public bool WaitFinish(TimeSpan timeout)
        {
            throw new NotImplementedException();
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

        public event EventHandler<IProtocolIdentifier>? _protocolWaitingTooLong;
        public event EventHandler<(int from, ConsensusMessage msg)>? _receivedExternalMessage;
        public event EventHandler<ConsensusMessage>? _messageBroadcasted;
        public event EventHandler<(int validator, ConsensusMessage msg)>? _messageSent;
    }
}