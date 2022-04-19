using System;
using Lachain.Proto;

namespace Lachain.Networking
{
    public interface INetworkManager : IDisposable
    {
        IMessageFactory MessageFactory { get; }
        void SendTo(ECDSAPublicKey publicKey, NetworkMessage message);
        void Start();
        void BroadcastLocalTransaction(TransactionReceipt receipt);
        void AdvanceEra(ulong era);
        Node LocalNode { get; }

        event EventHandler<(PingReply message, ECDSAPublicKey publicKey)>? OnPingReply;
        event EventHandler<(SyncBlocksRequest message, Action<SyncBlocksReply> callback)>? OnSyncBlocksRequest;
        event EventHandler<(SyncBlocksReply message, ECDSAPublicKey address)>? OnSyncBlocksReply;
        event EventHandler<(SyncPoolRequest message, Action<SyncPoolReply> callback)>? OnSyncPoolRequest;
        event EventHandler<(SyncPoolReply message, ECDSAPublicKey address)>? OnSyncPoolReply;
        event EventHandler<(ConsensusMessage message, ECDSAPublicKey publicKey)>? OnConsensusMessage;
        event EventHandler<(RootHashByTrieNameRequest message, Action<RootHashByTrieNameReply> callback)>? 
            OnRootHashByTrieNameRequest;
        event EventHandler<(RootHashByTrieNameReply message, ECDSAPublicKey address)>? OnRootHashByTrieNameReply;
        event EventHandler<(BlockBatchRequest message, Action<BlockBatchReply> callback)>? 
            OnBlockBatchRequest;
        event EventHandler<(BlockBatchReply message, ECDSAPublicKey address)>? OnBlockBatchReply;
        event EventHandler<(TrieNodeByHashRequest message, Action<TrieNodeByHashReply> callback)>? 
            OnTrieNodeByHashRequest;
        event EventHandler<(TrieNodeByHashReply message, ECDSAPublicKey address)>? OnTrieNodeByHashReply;
        event EventHandler<(CheckpointRequest message, Action<CheckpointReply> callback)>? OnCheckpointRequest;
        event EventHandler<(CheckpointReply message, ECDSAPublicKey address)>? OnCheckpointReply;
    }
}