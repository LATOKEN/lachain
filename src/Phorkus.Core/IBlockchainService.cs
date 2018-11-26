using System.Collections.Generic;
using Phorkus.Proto;

namespace Phorkus.Core
{
    public interface IBlockchainService
    {
        HandshakeReply Handshake(HandshakeRequest request);
        PingReply Ping(PingRequest request);
        
        IEnumerable<Block> GetBlocksByHashes(IEnumerable<UInt256> blockHashes);
        IEnumerable<UInt256> GetBlocksHashesByHeightRange(ulong fromBlock, ulong toBlock);
        
        IEnumerable<SignedTransaction> GetTransactionsByHashes(IEnumerable<UInt256> transactionHashes);
        IEnumerable<UInt256> GetTransactionHashesByBlockHeight(ulong blockHeight);
        
        IEnumerable<UInt256> GetMemoryPool();
        IEnumerable<Node> GetNeighbours();
    }
}