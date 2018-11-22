using Phorkus.Core.Consensus;
using Phorkus.Core.Network;
using Phorkus.Proto;

namespace Phorkus.Core.Messaging.Handlers
{
    public class ConsensusMessageHandler : IMessageHandler
    {
        private readonly IConsensusManager _consensusManager;

        public ConsensusMessageHandler(IConsensusManager consensusManager)
        {
            _consensusManager = consensusManager;
        }

        public void HandleMessage(IPeer peer, Message message)
        {
            if (message.BodyCase != Message.BodyOneofCase.ConsensusMessage) return;
            _consensusManager.HandleConsensusMessage(message.ConsensusMessage);
        }
    }
}