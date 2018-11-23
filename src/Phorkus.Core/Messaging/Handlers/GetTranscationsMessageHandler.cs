using System.Collections.Generic;
using Phorkus.Core.Blockchain.OperationManager;
using Phorkus.Core.Network;
using Phorkus.Proto;

namespace Phorkus.Core.Messaging.Handlers
{
    public class GetTranscationsMessageHandler : IMessageHandler
    {
        private readonly ITransactionManager _transactionManager;
        private readonly IMessageFactory _messageFactory;

        public GetTranscationsMessageHandler(
            ITransactionManager transactionManager,
            IMessageFactory messageFactory)
        {
            _transactionManager = transactionManager;
            _messageFactory = messageFactory;
        }

        public void HandleMessage(IPeer peer, Message message)
        {
            var getTransactions = message.GetTransactions;
            if (getTransactions is null)
                throw new InvalidMessageException();
            var txs = new List<SignedTransaction>();
            foreach (var hash in getTransactions.TransactionHashes)
            {
                var tx = _transactionManager.GetByHash(hash);
                if (tx is null)
                    continue;
                txs.Add(tx);
            }

            peer.Send(_messageFactory.TransactionsGotMessage(txs));
        }
    }
}