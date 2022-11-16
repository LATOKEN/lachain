using Lachain.Proto;

namespace Lachain.Consensus.RequestProtocols.Messages
{
    public interface IMessageResendHandler
    {
        RequestType Type { get; }
        void Terminate();
        void MessageReceived(int from, ConsensusMessage msg, RequestType type);
    }
}