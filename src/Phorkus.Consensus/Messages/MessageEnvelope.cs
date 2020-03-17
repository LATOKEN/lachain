using Phorkus.Proto;

namespace Phorkus.Consensus.Messages
{
    public class MessageEnvelope
    {
        public ConsensusMessage? ExternalMessage { get; }
        
        public int ValidatorIndex { get; }
        public IInternalMessage? InternalMessage { get; }

        public MessageEnvelope(ConsensusMessage msg, int validatorIndex)
        {
            ExternalMessage = msg;
            InternalMessage = null;
            ValidatorIndex = validatorIndex;
        }

        public MessageEnvelope(IInternalMessage msg, int validatorIndex)
        {
            InternalMessage = msg;
            ExternalMessage = null;
            ValidatorIndex = validatorIndex;
        }

        public bool External => !(ExternalMessage is null);
    }
}