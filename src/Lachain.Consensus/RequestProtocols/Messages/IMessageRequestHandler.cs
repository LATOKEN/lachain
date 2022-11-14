using System.Collections.Generic;
using Lachain.Proto;

namespace Lachain.Consensus.RequestProtocols.Messages
{
    public interface IMessageRequestHandler
    {
        RequestType Type { get; }
        void Terminate();
        void MessageReceived(int from, ConsensusMessage msg);
        bool IsProtocolComplete();
        List<(ConsensusMessage, int)> GetRequests(IProtocolIdentifier protocolId, int requestCount);
    }
}