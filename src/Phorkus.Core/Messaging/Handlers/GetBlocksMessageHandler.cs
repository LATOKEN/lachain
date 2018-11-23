using System;
using Phorkus.Core.Blockchain;
using Phorkus.Core.Network;
using Phorkus.Core.Storage;
using Phorkus.Proto;

namespace Phorkus.Core.Messaging.Handlers
{
    public class GetBlocksMessageHandler : IMessageHandler
    {
        private readonly IMessageFactory _messageFactory;
        private readonly IBlockchainContext _blockchainContext;
        private readonly IBlockRepository _blockRepository;
        
        public GetBlocksMessageHandler(
            IBlockchainContext blockchainContext,
            IBlockRepository blockRepository,
            IMessageFactory messageFactory)
        {
            _blockchainContext = blockchainContext;
            _blockRepository = blockRepository;
            _messageFactory = messageFactory;
        }
        
        public void HandleMessage(IPeer peer, Message message)
        {
            var getBlockHeader = message.GetBlocks;
            if (getBlockHeader is null)
                throw new InvalidMessageException();
            var usersHeight = (long) getBlockHeader.Height;
            var myHeight = (long) _blockchainContext.CurrentBlockHeaderHeight;
            var deltaHeight = myHeight - usersHeight;
            if (deltaHeight <= 0)
                return;
            var blocks = _blockRepository.GetBlocksByHeightRange((ulong) usersHeight, (ulong) Math.Min(deltaHeight, 1000));
            peer.Send(_messageFactory.BlocksGotMessage(blocks));
        }
    }
}