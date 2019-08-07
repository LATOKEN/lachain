using Phorkus.Proto;

namespace Phorkus.Consensus.Messages
{
    public class MessageEnvelope
    {
        public ConsensusMessage ConsensusMessage { get; }
        public IInternalMessage InternalMessage { get; }

        public MessageEnvelope(ConsensusMessage msg)
        {
            ConsensusMessage = msg;
            InternalMessage = null;
        }

        public MessageEnvelope(IInternalMessage msg)
        {
            InternalMessage = msg;
            ConsensusMessage = null;
        }

        public bool External => ConsensusMessage != null;
    }
}