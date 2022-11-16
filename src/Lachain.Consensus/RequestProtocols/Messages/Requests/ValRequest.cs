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

        public override void HandleReceivedMessage(int _from, ConsensusMessage msg)
        {
            if (msg.PayloadCase != ConsensusMessage.PayloadOneofCase.ValMessage)
                throw new Exception($"{msg.PayloadCase} message routed to Val request");
            MessageReceived(0, 0);
        }

        public override ConsensusMessage CreateConsensusMessage(IProtocolIdentifier protocolId, int _)
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
    }
}