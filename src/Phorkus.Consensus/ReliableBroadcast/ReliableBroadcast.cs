using System;
using Phorkus.Consensus.Messages;

namespace Phorkus.Consensus.ReliableBroadcast
{
    public class ReliableBroadcast : AbstractProtocol
    {
        public override IProtocolIdentifier Id { get; }
        public override void ProcessMessage(MessageEnvelope message)
        {
            throw new NotImplementedException();
        }
    }
}