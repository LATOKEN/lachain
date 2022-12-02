using System.Collections.Generic;
using Lachain.Proto;

namespace Lachain.Consensus.RequestProtocols.Messages
{
    public interface IMessageResendHandler
    {
        RequestType Type { get; }
        void Terminate();
        void MessageSent(int validator, ConsensusMessage msg, RequestType type);
        List<ConsensusMessage?> HandleRequest(int from, RequestConsensusMessage request, RequestType type);
    }
}