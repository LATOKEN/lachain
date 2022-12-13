using System;
using System.Collections.Generic;
using Lachain.Networking.Hub;
using Lachain.Proto;
using Lachain.Utility;

namespace Lachain.Networking
{
    public interface INetworkManager : IDisposable
    {
        IMessageFactory MessageFactory { get; }
        void SendTo(ECDSAPublicKey publicKey, NetworkMessage message, NetworkMessagePriority priority);
        void Start();
        void BroadcastLocalTransaction(TransactionReceipt receipt);
        void AdvanceEra(ulong era);
        void IncPenalty(ECDSAPublicKey publicKey);
        void BanPeer(byte[] publicKey);
        void RemoveFromBanList(byte[] publicKey);
        Node LocalNode { get; }

        event EventHandler<(PingReply message, ECDSAPublicKey publicKey)>? OnPingReply;
        event EventHandler<(SyncBlocksRequest message, Action<SyncBlocksReply> callback)>? OnSyncBlocksRequest;
        event EventHandler<(SyncBlocksReply message, ECDSAPublicKey address)>? OnSyncBlocksReply;
        event EventHandler<(SyncPoolReply message, ECDSAPublicKey address)>? OnSyncPoolReply;
        event EventHandler<(ConsensusMessage message, ECDSAPublicKey publicKey)>? OnConsensusMessage;
        event EventHandler<ulong>? OnAdvanceEra;
        event EventHandler<ClientWorker>? OnWorkerCreated;
    }
}