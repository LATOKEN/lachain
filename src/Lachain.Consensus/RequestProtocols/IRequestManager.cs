using Lachain.Proto;

namespace Lachain.Consensus.RequestProtocols
{
    public interface IRequestManager
    {
        void Terminate();
        void SetValidators(int validatorsCount);
        void RegisterProtocol(IProtocolIdentifier protocolId);
    }
}