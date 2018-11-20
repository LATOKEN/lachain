using System.Collections.Generic;
using Phorkus.Core.Blockchain.Consensus;
using Phorkus.Core.Messaging.Handlers;
using Phorkus.Core.Network;
using Phorkus.Core.Network.Proto;

namespace Phorkus.Core.Messaging
{
    public class MessagingManager : IMessagingManager
    {
        private readonly IReadOnlyDictionary<MessageType, IMessageHandler> _messageHandlers;
        
        public MessagingManager(IConsensusManager consensusManager)
        {
            _messageHandlers = new Dictionary<MessageType, IMessageHandler>
            {
                { MessageType.BlockHeadersGot, new BlockHeadersGotMessageHandler() },
                { MessageType.BlocksGot, new BlocksGotMessageHandler() },
                { MessageType.HandshakeResponse, new HandshakeResponseMessageHandler() },
                { MessageType.MempoolGot, new MempoolGotMessageHandler() },
                { MessageType.TransactionsGot, new TransactionsGotMessageHandler() },
                { MessageType.NeighboursGot, new NeighboursGotMessageHandler() },
                { MessageType.ConsensusMessage, new ConsensusMessageHandler(consensusManager) }
            };
        }
        
        public void HandleMessage(IPeer peer, Message message)
        {
            var handler = _messageHandlers[message.Type];
            if (handler is null)
                throw new InvalidMessageTypeException();
            handler.HandleMessage(peer, message);
        }
    }
}