using Phorkus.Proto;

namespace Phorkus.Networking
{
    public interface IMessageHandler
    {
        void HandshakeRequest(HandshakeRequest request);
        void HandshakeReply(HandshakeReply reply);
        void GetBlocksByHashesRequest(GetBlocksByHashesRequest request);
        void GetBlocksByHashesReply(GetBlocksByHashesReply reply);
        void GetBlocksByHeightRangeRequest(GetBlocksByHeightRangeRequest request);
        void GetBlocksByHeightRangeReply(GetBlocksByHeightRangeReply reply);
        void GetTransactionsByHashesRequest(GetTransactionsByHashesRequest request);
        void GetTransactionsByHashesReply(GetTransactionsByHashesReply reply);
    }
}