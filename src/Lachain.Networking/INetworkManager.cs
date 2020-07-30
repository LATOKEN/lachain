using System;
using System.Collections.Generic;
using Lachain.Proto;

namespace Lachain.Networking
{
    public interface INetworkManager
    {
        IMessageFactory? MessageFactory { get; }
        bool IsConnected(PeerAddress address);
        IRemotePeer? Connect(PeerAddress address);
        void SendToPeerByPublicKey(ECDSAPublicKey publicKey, NetworkMessage message);
        bool IsReady { get; }
        void Start();
        void Stop();
        void BroadcastLocalTransaction(TransactionReceipt receipt);

        event EventHandler<(PingRequest message, Action<PingReply> callback)>? OnPingRequest;
        event EventHandler<(PingReply message, ECDSAPublicKey publicKey)>? OnPingReply;

        event EventHandler<(GetBlocksByHashesRequest message, Action<GetBlocksByHashesReply> callback)>?
            OnGetBlocksByHashesRequest;

        event EventHandler<(GetBlocksByHashesReply message, ECDSAPublicKey address)>? OnGetBlocksByHashesReply;

        event EventHandler<(GetBlocksByHeightRangeRequest message, Action<GetBlocksByHeightRangeReply> callback)>?
            OnGetBlocksByHeightRangeRequest;

        event EventHandler<(GetPeersRequest message, Action<GetPeersReply> callback)>?
            OnGetPeersRequest;

        event EventHandler<(GetBlocksByHeightRangeReply message, Action<GetBlocksByHashesRequest> callback)>?
            OnGetBlocksByHeightRangeReply;

        event EventHandler<(GetTransactionsByHashesRequest message, Action<GetTransactionsByHashesReply> callback)>?
            OnGetTransactionsByHashesRequest;

        event EventHandler<(GetTransactionsByHashesReply message, ECDSAPublicKey address)>?
            OnGetTransactionsByHashesReply;

        event EventHandler<(GetPeersReply message, ECDSAPublicKey address, Func<PeerAddress, IRemotePeer> connect)>?
            OnGetPeersReply;

        event EventHandler<(ConsensusMessage message, ECDSAPublicKey publicKey)>? OnConsensusMessage;
        IEnumerable<PeerAddress> GetConnectedPeers();
        void ConnectToValidators(IEnumerable<ECDSAPublicKey> validators);
        string CheckLocalConnection(string host);
    }
}