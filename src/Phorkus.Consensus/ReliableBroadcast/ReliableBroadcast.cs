using System;
using Phorkus.Consensus.Messages;

namespace Phorkus.Consensus.ReliableBroadcast
{
    public class ReliableBroadcast : AbstractProtocol
    {
        public ReliableBroadcast(ReliableBroadcastId reliableBroadcastId, IConsensusBroadcaster broadcaster) : base(
            broadcaster)
        {
            throw new NotImplementedException();
        }
        public override IProtocolIdentifier Id { get; }
        public override void ProcessMessage(MessageEnvelope envelope)
        {
            throw new NotImplementedException();
        }
    }
}