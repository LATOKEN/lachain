using System;
using Lachain.Proto;

namespace Lachain.Networking
{
    public interface INetworkManager
    {
        IMessageFactory? MessageFactory { get; }
        bool IsConnected(PeerAddress address);
        IRemotePeer? Connect(PeerAddress address);
        IRemotePeer? GetPeerByPublicKey(ECDSAPublicKey publicKey);
        bool IsReady { get; }
        void Start();
        void Stop();
        void BroadcastLocalTransaction(TransactionReceipt receipt);

        event EventHandler<(MessageEnvelope envelope, PingRequest message)>? OnPingRequest;
        event EventHandler<(MessageEnvelope envelope, PingReply message)>? OnPingReply;
        event EventHandler<(MessageEnvelope envelope, GetBlocksByHashesRequest message)>? OnGetBlocksByHashesRequest;
        event EventHandler<(MessageEnvelope envelope, GetBlocksByHashesReply message)>? OnGetBlocksByHashesReply;

        event EventHandler<(MessageEnvelope envelope, GetBlocksByHeightRangeRequest message)>?
            OnGetBlocksByHeightRangeRequest;

        event EventHandler<(MessageEnvelope envelope, GetBlocksByHeightRangeReply message)>?
            OnGetBlocksByHeightRangeReply;

        event EventHandler<(MessageEnvelope envelope, GetTransactionsByHashesRequest message)>?
            OnGetTransactionsByHashesRequest;

        event EventHandler<(MessageEnvelope envelope, GetTransactionsByHashesReply message)>?
            OnGetTransactionsByHashesReply;

        event EventHandler<(MessageEnvelope envelope, ConsensusMessage message)>? OnConsensusMessage;
    }
}