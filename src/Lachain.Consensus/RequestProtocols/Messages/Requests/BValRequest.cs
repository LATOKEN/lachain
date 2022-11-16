using System;
using Lachain.Consensus.BinaryAgreement;
using Lachain.Proto;

namespace Lachain.Consensus.RequestProtocols.Messages.Requests
{
    public class BValRequest : MessageRequestHandler
    {
        public BValRequest(RequestType type, int validatorCount, int msgPerValidator)
            : base(type, validatorCount, msgPerValidator)
        {

        }

        protected override void HandleReceivedMessage(int from, ConsensusMessage msg)
        {
            if (msg.PayloadCase != ConsensusMessage.PayloadOneofCase.Bval)
                throw new Exception($"{msg.PayloadCase} message routed to Bval request");
            MessageReceived(from, msg.Bval.Value ? 1 : 0);
        }

        protected override ConsensusMessage CreateConsensusMessage(IProtocolIdentifier protocolId, int _)
        {
            var id = protocolId as BinaryBroadcastId ?? throw new Exception($"wrong protcolId {protocolId} for Bval request");
            var bvalRequest = new RequestBValMessage
            {
                Agreement = (int) id.Agreement,
                Epoch = (int) id.Epoch
            };
            return new ConsensusMessage
            {
                RequestConsensus = new RequestConsensusMessage
                {
                    RequestBval = bvalRequest
                }
            };
        }
    }
}