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
        /// <summary>
        /// Formats a NetworkMessage to request blocks in batch
        /// </summary>
        /// <param name = "blockNumbers"> Block id </param>
        /// <param name = "requestId"> Request Id </param>
        /// <returns>
        /// NetworkMessage
        /// </returns>
        NetworkMessage BlockBatchRequest(List<ulong> blockNumbers, ulong requestId);
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
        /// <returns>
        /// NetworkMessage
        /// </returns>
        NetworkMessage CheckpointRequest(byte[] request);

        MessageBatch MessagesBatch(IEnumerable<NetworkMessage> messages);

        byte[] SignCommunicationHubInit(byte[] hubKey);
    }
}