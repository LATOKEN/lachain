using Lachain.Proto;

namespace Lachain.Consensus.RequestProtocols.Protocols
{
    public interface IProtocolResendHandler
    {
        void Terminate();
    }
}