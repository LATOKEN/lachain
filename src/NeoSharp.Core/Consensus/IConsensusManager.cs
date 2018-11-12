using NeoSharp.Core.Messaging.Messages;

namespace NeoSharp.Core.Consensus
{
    public interface IConsensusManager
    {
        void Start();
        void HandleConsensusMessage(ConsensusMessage message);
    }
}