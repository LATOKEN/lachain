using System.Collections.Generic;
using Phorkus.Core.Handling.Handlers;
using Phorkus.Core.Network;
using Phorkus.Core.Network.Proto;

namespace Phorkus.Core.Handling
{
    public class HandlingManager : IHandlingManager
    {
        private readonly IReadOnlyDictionary<MessageType, IMessageHandler> _messageHandlers;

        internal HandlingManager()
        {
            _messageHandlers = new Dictionary<MessageType, IMessageHandler>
            {
                { MessageType.BlockHeadersGot, new BlockHeadersGotMessageHandler() },
                { MessageType.BlocksGot, new BlocksGotMessageHandler() },
                { MessageType.HandshakeResponse, new HandshakeResponseMessageHandler() },
                { MessageType.MempoolGot, new MempoolGotMessageHandler() },
                { MessageType.TransactionsGot, new TransactionsGotMessageHandler() },
                { MessageType.NeighboursGot, new NeighboursGotMessageHandler() }
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