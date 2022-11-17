using System;
using Lachain.Proto;

namespace Lachain.Consensus.RequestProtocols
{
    public interface IRequestManager : IDisposable
    {
        void Terminate();
        void SetValidators(int validatorsCount);
        void RegisterProtocol(IProtocolIdentifier protocolId, IConsensusProtocol protocol);
        void HandleRequest(int from, ConsensusMessage request);
    }
}