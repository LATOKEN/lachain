using System;
using Phorkus.Proto;

namespace Phorkus.Consensus
{
    public interface IConsensusProtocol
    {
        IProtocolIdentifier Id { get; }
        void HandleMessage(ConsensusMessage message);
        event EventHandler Terminated;
    }
}