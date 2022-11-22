using System;
using Lachain.Consensus.BinaryAgreement;
using Lachain.Proto;

namespace Lachain.Consensus.RequestProtocols.Messages.Requests
{
    public class AuxRequest : MessageRequestHandler
    {
        public AuxRequest(RequestType type, int validatorCount, int msgPerValidator)
            : base(type, validatorCount, msgPerValidator)
        {

        }

        protected override void HandleReceivedMessage(int from, ConsensusMessage msg)
        {
            if (msg.PayloadCase != ConsensusMessage.PayloadOneofCase.Aux)
                throw new Exception($"{msg.PayloadCase} message routed to Aux request");
            MessageReceived(from, 0);
        }

        public override ConsensusMessage CreateConsensusRequestMessage(IProtocolIdentifier protocolId, int _)
        {
            var id = protocolId as BinaryBroadcastId ?? throw new Exception($"wrong protcolId {protocolId} for Aux request");
            var auxRequest = new RequestAuxMessage
            {
                Agreement = (int) id.Agreement,
                Epoch = (int) id.Epoch
            };
            return new ConsensusMessage
            {
                RequestConsensus = new RequestConsensusMessage
                {
                    RequestAux = auxRequest
                }
            };
        }

        public static BinaryBroadcastId CreateProtocolId(RequestConsensusMessage msg, long era)
        {
            if (msg.PayloadCase != RequestConsensusMessage.PayloadOneofCase.RequestAux)
                throw new Exception($"{msg.PayloadCase} routed to Aux Request");
            return new BinaryBroadcastId(era, msg.RequestAux.Agreement, msg.RequestAux.Epoch);
        }
    }
}