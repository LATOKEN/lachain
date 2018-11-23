using System.Collections.Generic;
using Phorkus.Proto;

namespace Phorkus.Core.Messaging
{
    public class MessageFactory : IMessageFactory
    {
        public Message GetBlocksMessage(ulong height)
        {
            var getBlocksMessage = new GetBlocksMessage
            {
                Height = height
            };
            return new Message
            {
                Type = MessageType.GetBlocks,
                GetBlocks = getBlocksMessage
            };
        }

        public Message BlocksGotMessage(IEnumerable<Block> blocks)
        {
            var blocksGotMessage = new BlocksGotMessage();
            blocksGotMessage.Blocks.AddRange(blocks);
            return new Message
            {
                Type = MessageType.BlocksGot,
                BlocksGot = blocksGotMessage
            };
        }

        public Message GetTransactionsMessage(IEnumerable<UInt256> hashes)
        {
            var getTransactionsMessage = new GetTransactionsMessage();
            getTransactionsMessage.TransactionHashes.AddRange(hashes);
            return new Message
            {
                Type = MessageType.GetTransactions,
                GetTransactions = getTransactionsMessage
            };
        }

        public Message TransactionsGotMessage(IEnumerable<SignedTransaction> transactions)
        {
            var transactionsGotMessage = new TransactionsGotMessage();
            transactionsGotMessage.Transactions.AddRange(transactions);
            return new Message
            {
                Type = MessageType.TransactionsGot,
                TransactionsGot = transactionsGotMessage
            };
        }
    }
}