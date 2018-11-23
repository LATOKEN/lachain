using Phorkus.Core.Network;
using Phorkus.Proto;

namespace Phorkus.Core.Messaging.Handlers
{
    public class BlocksGotMessageHandler : IMessageHandler
    {
        private readonly IBlockchainSynchronizer _blockchainSynchronizer;

        public BlocksGotMessageHandler(
            IBlockchainSynchronizer blockchainSynchronizer)
        {
            _blockchainSynchronizer = blockchainSynchronizer;
        }
        
        public void HandleMessage(IPeer peer, Message message)
        {
            var blocksGot = message.BlocksGot;
            if (blocksGot is null)
                throw new InvalidMessageException();
            foreach (var block in blocksGot.Blocks)
                _blockchainSynchronizer.HandleBlockFromPeer(block, peer);
        }
    }
}