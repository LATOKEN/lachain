using System;
using Lachain.Proto;

namespace Lachain.Consensus.RequestProtocols.Messages
{
    public interface IMessageRequestHandler
    {
        RequestType Type { get; }
        int RemainingMsgCount { get; }
        void Terminate();
        Tuple<int, int, ulong, MessageStatus>? Peek();
        void Dequeue();
        void Enqueue(int validatorId, int msgId, ulong requestTime);
        void MessageRequested(int validatorId, int msgId);
        void MessageReceived(int from, ConsensusMessage msg, RequestType type);
        bool IsProtocolComplete();
        ConsensusMessage CreateConsensusRequestMessage(IProtocolIdentifier protocolId, int msgId);
    }
}