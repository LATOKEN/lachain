using System.Collections.Generic;
using Lachain.Proto;

namespace Lachain.Networking
{
    public interface IMessageFactory
    {
        ECDSAPublicKey GetPublicKey();

        NetworkMessage Ack(ulong messageId);
        NetworkMessage PingReply(ulong timestamp, ulong blockHeight);
        NetworkMessage ConsensusMessage(ConsensusMessage message);
        NetworkMessage GetPeersRequest();
        NetworkMessage GetPeersReply(Peer[] peers);
        NetworkMessage SyncPoolRequest(IEnumerable<UInt256> hashes);
        NetworkMessage SyncPoolReply(IEnumerable<TransactionReceipt> transactions);
        NetworkMessage SyncBlocksRequest(ulong fromHeight, ulong toHeight);
        /// Formats a NetworkMessage to request the root hash of a snapshot
        /// </summary>
        /// <param name = "block"> Block id </param>
        /// <param name = "trieName"> Trie Name </param>
        /// <returns>
        /// NetworkMessage
        /// </returns>
        NetworkMessage RootHashByTrieNameRequest(ulong block, string trieName, ulong requestId);
        /// <summary>
        /// Formats a NetworkMessage to request the root hash of a snapshot
        /// </summary>
        /// <param name = "block"> Block id </param>
        /// <param name = "trieName"> Trie Name </param>
        /// <returns>
        /// NetworkMessage
        /// </returns>
        NetworkMessage RootHashByTrieNameRequest(ulong block, string trieName, ulong requestId);
        /// <summary>
        /// Formats a NetworkMessage to request blocks in batch
        /// </summary>
        /// <param name = "fromBlock"> First block id of the batch </param>
        /// <param name = "toBlock"> Last block id of the batch </param>
        /// <param name = "requestId"> Request Id </param>
        /// <returns>
        /// NetworkMessage
        /// </returns>
        NetworkMessage BlockBatchRequest(ulong fromBlock, ulong toBlock, ulong requestId);
        /// <summary>
        /// Formats a NetworkMessage to request trie-nodes in batch
        /// </summary>
        /// <param name = "nodeHashes"> Node hash </param>
        /// <param name = "requestId"> Request Id </param>
        /// <returns>
        /// NetworkMessage
        /// </returns>
        NetworkMessage TrieNodeByHashRequest(List<UInt256> nodeHashes, ulong requestId);
        /// <summary>
        /// Formats a NetworkMessage to request checkpoint info
        /// </summary>
        /// <param name = "request"> Array of bytes consisting of CheckpointType </param>
        /// <param name = "requestId"> Request Id </param>
        /// <returns>
        /// NetworkMessage
        /// </returns>
        NetworkMessage CheckpointRequest(byte[] request, ulong requestId);
         /// <summary>
        /// Formats a NetworkMessage to request checkpoint block without transaction receipts
        /// </summary>
        /// <param name = "blockNumber"> Block height of the checkpoint block </param>
        /// <param name = "requestId"> Request Id </param>
        /// <returns>
        /// NetworkMessage
        /// </returns>
        NetworkMessage CheckpointBlockRequest(ulong blockNumber, ulong requestId);

        MessageBatch MessagesBatch(IEnumerable<NetworkMessage> messages);

        byte[] SignCommunicationHubInit(byte[] hubKey);
    }
}