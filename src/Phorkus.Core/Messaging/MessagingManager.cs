using System.Collections.Generic;
using Phorkus.Core.Blockchain;
using Phorkus.Core.Blockchain.OperationManager;
using Phorkus.Core.Consensus;
using Phorkus.Core.Logging;
using Phorkus.Core.Messaging.Handlers;
using Phorkus.Core.Network;
using Phorkus.Core.Storage;
using Phorkus.Proto;

namespace Phorkus.Core.Messaging
{
    public class MessagingManager : IMessagingManager
    {
        private readonly IReadOnlyDictionary<MessageType, IMessageHandler> _messageHandlers;
        
        public MessagingManager(
            IConsensusManager consensusManager,
            IMessageFactory messageFactory,
            IBlockchainContext blockchainContext,
            IBlockRepository blockRepository,
            ILogger<IMessagingManager> messagingLogger,
            IBlockManager blockManager,
            IBlockchainSynchronizer blockchainSynchronizer,
            ITransactionManager transactionManager)
        {
            _messageHandlers = new Dictionary<MessageType, IMessageHandler>
            {
                { MessageType.GetBlocks, new GetBlocksMessageHandler(blockchainContext, blockRepository, messageFactory) },
                { MessageType.BlocksGot, new BlocksGotMessageHandler(blockchainSynchronizer) },
                { MessageType.HandshakeRequest, new HandshakeRequestMessageHandler() },
                { MessageType.HandshakeResponse, new HandshakeResponseMessageHandler(messageFactory, blockchainContext) },
                { MessageType.MempoolGot, new MempoolGotMessageHandler() },
                { MessageType.TransactionsGot, new TransactionsGotMessageHandler(blockchainSynchronizer) },
                { MessageType.GetTransactions, new GetTranscationsMessageHandler(transactionManager, messageFactory) },
                { MessageType.NeighboursGot, new NeighboursGotMessageHandler() },
                { MessageType.ConsensusMessage, new ConsensusMessageHandler(consensusManager) }
            };
        }
        
        public bool HandleMessage(IPeer peer, Message message)
        {
            if (!_messageHandlers.TryGetValue(message.Type, out var handler))
                return false;
            handler.HandleMessage(peer, message);
            //Task.Factory.StartNew(() => );
            return true;
        }
    }
}