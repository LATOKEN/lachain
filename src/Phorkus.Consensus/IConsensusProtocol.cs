using System;
using Phorkus.Consensus.Messages;
using Phorkus.Proto;

namespace Phorkus.Consensus
{
    public interface IConsensusProtocol
    {
        IProtocolIdentifier Id { get; }
        void HandleMessage(ConsensusMessage message);
        void HandleInternalMessage(InternalMessage message);
    }
}