using System;
using System.Net;
using Lachain.Networking.ZeroMQ;
using Lachain.Proto;

namespace Lachain.Networking
{
    public interface INetworkManager : IDisposable
    {
        IMessageFactory MessageFactory { get; }
        void SendTo(ECDSAPublicKey publicKey, NetworkMessage message);
        void Start();
        void BroadcastLocalTransaction(TransactionReceipt receipt);
        void AdvanceEra(long era);
        string CheckLocalConnection(string host);
        public bool IsSelfConnect(IPAddress ipAddress);
        Node LocalNode { get; }

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

        event EventHandler<(GetPeersReply message, ECDSAPublicKey address, Func<ECDSAPublicKey, ClientWorker> connect)>?
            OnGetPeersReply;

        event EventHandler<(ConsensusMessage message, ECDSAPublicKey publicKey)>? OnConsensusMessage;
    }
}