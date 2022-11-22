using System;
using Lachain.Consensus.ReliableBroadcast;
using Lachain.Proto;

namespace Lachain.Consensus.RequestProtocols.Messages.Requests
{
    public class EchoRequest : MessageRequestHandler
    {
        public EchoRequest(RequestType type, int validatorCount, int msgPerValidator)
            : base(type, validatorCount, msgPerValidator)
        {

        }

        protected override void HandleReceivedMessage(int from, ConsensusMessage msg)
        {
            if (msg.PayloadCase != ConsensusMessage.PayloadOneofCase.EchoMessage)
                throw new Exception($"{msg.PayloadCase} message routed to Echo request");
            MessageReceived(from, 0);
        }

        public override ConsensusMessage CreateConsensusRequestMessage(IProtocolIdentifier protocolId, int _)
        {
            var id = protocolId as ReliableBroadcastId ?? throw new Exception($"wrong protcolId {protocolId} for Echo request");
            var echoRequest = new RequestECHOMessage
            {
                SenderId = id.SenderId
            };
            return new ConsensusMessage
            {
                RequestConsensus = new RequestConsensusMessage
                {
                    RequestEcho = echoRequest
                }
            };
        }

        public static ReliableBroadcastId CreateProtocolId(RequestConsensusMessage msg, long era)
        {
            if (msg.PayloadCase != RequestConsensusMessage.PayloadOneofCase.RequestEcho)
                throw new Exception($"{msg.PayloadCase} routed to Echo Request");
            return new ReliableBroadcastId(msg.RequestEcho.SenderId, (int) era);
        }
    }
}