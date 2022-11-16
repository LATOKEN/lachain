using System;
using Lachain.Consensus.ReliableBroadcast;
using Lachain.Proto;

namespace Lachain.Consensus.RequestProtocols.Messages.Requests
{
    public class ReadyRequest : MessageRequestHandler
    {
        public ReadyRequest(RequestType type, int validatorCount, int msgPerValidator)
            : base(type, validatorCount, msgPerValidator)
        {

        }

        public override void HandleReceivedMessage(int from, ConsensusMessage msg)
        {
            if (msg.PayloadCase != ConsensusMessage.PayloadOneofCase.ReadyMessage)
                throw new Exception($"{msg.PayloadCase} message routed to Ready request");
            MessageReceived(from, 0);
        }

        public override ConsensusMessage CreateConsensusMessage(IProtocolIdentifier protocolId, int _)
        {
            var id = protocolId as ReliableBroadcastId ?? throw new Exception($"wrong protcolId {protocolId} for Ready request");
            var readyRequest = new RequestReadyMessage
            {
                SenderId = id.SenderId
            };
            return new ConsensusMessage
            {
                RequestConsensus = new RequestConsensusMessage
                {
                    RequestReady = readyRequest
                }
            };
        }
    }
}