using NeoSharp.Core.Messaging.Messages;

namespace NeoSharp.Core.Consensus
{
    public interface IConsensusManager
    {
        void HandleConsensusMessage(ConsensusMessage message);
        void InitializeConsensus(byte viewNumber);
    }
}