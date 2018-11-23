using Phorkus.Core.Network;
using Phorkus.Proto;

namespace Phorkus.Core.Messaging.Handlers
{
    public class TransactionsGotMessageHandler : IMessageHandler
    {
        private readonly IBlockchainSynchronizer _blockchainSynchronizer;

        public TransactionsGotMessageHandler(IBlockchainSynchronizer blockchainSynchronizer)
        {
            _blockchainSynchronizer = blockchainSynchronizer;
        }

        public void HandleMessage(IPeer peer, Message message)
        {
            var transactionsGot = message.TransactionsGot;
            if (transactionsGot is null)
                throw new InvalidMessageException();
            _blockchainSynchronizer.HandleTransactionsFromPeer(transactionsGot.Transactions, peer);
        }
    }
}