using Phorkus.Proto;

namespace Phorkus.Consensus.Messages
{
    public class MessageEnvelope
    {
        public ConsensusMessage ExternalMessage { get; }
        public IInternalMessage InternalMessage { get; }

        public MessageEnvelope(ConsensusMessage msg)
        {
            ExternalMessage = msg;
            InternalMessage = null;
        }

        public MessageEnvelope(IInternalMessage msg)
        {
            InternalMessage = msg;
            ExternalMessage = null;
        }

        public bool External => ExternalMessage != null;
    }
}