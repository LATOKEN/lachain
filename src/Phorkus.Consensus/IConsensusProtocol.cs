using System;
using Phorkus.Proto;

namespace Phorkus.Consensus
{
    public interface IConsensusProtocol
    {
        void HandleMessage(ConsensusMessage message);
        event EventHandler Terminated;
    }
}