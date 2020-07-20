using System.Collections.Generic;
using Lachain.Proto;

namespace Lachain.Networking
{
    public interface IMessageFactory
    {
        NetworkMessage Ack(ulong messageId);
        NetworkMessage HandshakeRequest(Node node);

        NetworkMessage HandshakeReply(Node node, int port);

        NetworkMessage PingRequest(ulong timestamp, ulong blockHeight);

        NetworkMessage PingReply(ulong timestamp, ulong blockHeight);
        
        NetworkMessage ConsensusMessage(ConsensusMessage message);

        NetworkMessage GetBlocksByHashesRequest(IEnumerable<UInt256> blockHashes);

        NetworkMessage GetBlocksByHashesReply(IEnumerable<Block> blocks);

        NetworkMessage GetBlocksByHeightRangeRequest(ulong fromHeight, ulong toHeight);

        NetworkMessage GetBlocksByHeightRangeReply(IEnumerable<UInt256> blockHashes);

        NetworkMessage GetTransactionsByHashesRequest(IEnumerable<UInt256> transactionHashes);

        NetworkMessage GetTransactionsByHashesReply(IEnumerable<TransactionReceipt> transactions);
        
        MessageBatch MessagesBatch(IEnumerable<NetworkMessage> messages);
    }
}