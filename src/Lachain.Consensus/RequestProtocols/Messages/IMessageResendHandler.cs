using Lachain.Proto;

namespace Lachain.Consensus.RequestProtocols.Messages
{
    public interface IMessageResendHandler
    {
        RequestType Type { get; }
        void Terminate();
        void MessageSent(int validator, ConsensusMessage msg, RequestType type);
    }
}