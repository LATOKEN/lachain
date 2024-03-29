using System;
using Lachain.Consensus.BinaryAgreement;
using Lachain.Proto;

namespace Lachain.Consensus.RequestProtocols.Messages.Requests
{
    public class ConfRequest : MessageRequestHandler
    {
        public ConfRequest(RequestType type, int validatorCount, int msgPerValidator)
            : base(type, validatorCount, msgPerValidator)
        {

        }

        protected override void HandleReceivedMessage(int from, ConsensusMessage msg)
        {
            if (msg.PayloadCase != ConsensusMessage.PayloadOneofCase.Conf)
                throw new Exception($"{msg.PayloadCase} message routed to Conf request");
            MessageReceived(from, 0);
        }

        public override ConsensusMessage CreateConsensusRequestMessage(IProtocolIdentifier protocolId, int _)
        {
            var id = protocolId as BinaryBroadcastId ?? throw new Exception($"wrong protcolId {protocolId} for Conf request");
            var confRequest = new RequestConfMessage
            {
                Agreement = (int) id.Agreement,
                Epoch = (int) id.Epoch
            };
            return new ConsensusMessage
            {
                RequestConsensus = new RequestConsensusMessage
                {
                    RequestConf = confRequest
                }
            };
        }

        public static BinaryBroadcastId CreateProtocolId(RequestConsensusMessage msg, long era)
        {
            if (msg.PayloadCase != RequestConsensusMessage.PayloadOneofCase.RequestConf)
                throw new Exception($"{msg.PayloadCase} routed to Conf Request");
            return new BinaryBroadcastId(era, msg.RequestConf.Agreement, msg.RequestConf.Epoch);
        }
    }
}