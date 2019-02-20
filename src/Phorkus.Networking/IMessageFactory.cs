using System.Collections.Generic;
using Phorkus.Proto;

namespace Phorkus.Networking
{
    public interface IMessageFactory
    {
        NetworkMessage HandshakeRequest(Node node);

        NetworkMessage HandshakeReply(Node node);

        NetworkMessage PingRequest(ulong timestamp, ulong blockHeight);

        NetworkMessage PingReply(ulong timestamp, ulong blockHeight);
        
        NetworkMessage ConsensusMessage(ConsensusMessage message);

        NetworkMessage GetBlocksByHashesRequest(IEnumerable<UInt256> blockHashes);

        NetworkMessage GetBlocksByHashesReply(IEnumerable<Block> blocks);

        NetworkMessage GetBlocksByHeightRangeRequest(ulong fromHeight, ulong toHeight);

        NetworkMessage GetBlocksByHeightRangeReply(IEnumerable<UInt256> blockHashes);

        NetworkMessage GetTransactionsByHashesRequest(IEnumerable<UInt256> transactionHashes);

        NetworkMessage GetTransactionsByHashesReply(IEnumerable<TransactionReceipt> transactions);
        
        NetworkMessage ThresholdRequest(byte[] message);

        NetworkMessage ChangeViewRequest();
        
        NetworkMessage BlockPrepareRequest();
        
        NetworkMessage BlockPrepareReply();
    }
}