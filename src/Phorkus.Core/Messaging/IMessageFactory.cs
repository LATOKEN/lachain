using System.Collections.Generic;
using Phorkus.Proto;

namespace Phorkus.Core.Messaging
{
    public interface IMessageFactory
    {
        Message GetBlocksMessage(ulong height);

        Message BlocksGotMessage(IEnumerable<Block> blocks);

        Message GetTransactionsMessage(IEnumerable<UInt256> hashes);
        
        Message TransactionsGotMessage(IEnumerable<SignedTransaction> transactions);

        Message HandshakeResponse(Node node);
    }
}