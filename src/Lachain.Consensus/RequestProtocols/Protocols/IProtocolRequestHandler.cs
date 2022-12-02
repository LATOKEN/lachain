using System.Collections.Generic;
using Lachain.Proto;

namespace Lachain.Consensus.RequestProtocols.Protocols
{
    public interface IProtocolRequestHandler
    {
        void Terminate();
        List<(ConsensusMessage, int)> GetRequests(int requestCount);
    }
}