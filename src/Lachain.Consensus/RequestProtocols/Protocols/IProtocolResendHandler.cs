using System.Collections.Generic;
using Lachain.Consensus.RequestProtocols.Messages;
using Lachain.Proto;

namespace Lachain.Consensus.RequestProtocols.Protocols
{
    public interface IProtocolResendHandler
    {
        void Terminate();
        List<ConsensusMessage> HandleRequest(int from, RequestConsensusMessage request, RequestType requestType);
    }
}