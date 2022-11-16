using System;
using Lachain.Consensus.ReliableBroadcast;
using Lachain.Proto;

namespace Lachain.Consensus.RequestProtocols.Messages.Requests
{
    public class ValRequest : MessageRequestHandler
    {
        public ValRequest(RequestType type, int validatorCount, int msgPerValidator)
            : base(type, validatorCount, msgPerValidator)
        {

        }

        protected override void HandleReceivedMessage(int _from, ConsensusMessage msg)
        {
            if (msg.PayloadCase != ConsensusMessage.PayloadOneofCase.ValMessage)
                throw new Exception($"{msg.PayloadCase} message routed to Val request");
            MessageReceived(0, 0);
        }

        protected override ConsensusMessage CreateConsensusMessage(IProtocolIdentifier protocolId, int _)
        {
            var id = protocolId as ReliableBroadcastId ?? throw new Exception($"wrong protcolId {protocolId} for Val request");
            var valRequest = new RequestValMessage
            {
                SenderId = id.SenderId
            };
            return new ConsensusMessage
            {
                RequestConsensus = new RequestConsensusMessage
                {
                    RequestVal = valRequest
                }
            };
        }

        public static ReliableBroadcastId CreateProtocolId(RequestConsensusMessage msg, long era)
        {
            if (msg.PayloadCase != RequestConsensusMessage.PayloadOneofCase.RequestVal)
                throw new Exception($"{msg.PayloadCase} routed to Val Request");
            return new ReliableBroadcastId(msg.RequestVal.SenderId, (int) era);
        }
    }
}