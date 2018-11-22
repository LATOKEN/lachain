using Phorkus.Core.Blockchain.Consensus;
using Phorkus.Core.Network;
using Phorkus.Core.Network.Proto;

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