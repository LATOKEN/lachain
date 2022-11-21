using System.Collections.Generic;
using Lachain.Proto;

namespace Lachain.Consensus.RequestProtocols.Messages
{
    public interface IMessageRequestHandler
    {
        RequestType Type { get; }
        int RemainingMsgCount { get; }
        void Terminate();
        void MessageReceived(int from, ConsensusMessage msg, RequestType type);
        bool IsProtocolComplete();
        List<(ConsensusMessage, int)> GetNewRequests(IProtocolIdentifier protocolId, int requestCount);
        List<(ConsensusMessage, int)> GetRequests(IProtocolIdentifier protocolId, int requestCount);
    }
}