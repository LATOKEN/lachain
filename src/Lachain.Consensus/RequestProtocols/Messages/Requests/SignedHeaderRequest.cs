using System;
using Lachain.Consensus.RootProtocol;
using Lachain.Proto;

namespace Lachain.Consensus.RequestProtocols.Messages.Requests
{
    public class SignedHeaderRequest : MessageRequestHandler
    {
        public SignedHeaderRequest(RequestType type, int validatorCount, int msgPerValidator)
            : base(type, validatorCount, msgPerValidator)
        {

        }

        public override void HandleReceivedMessage(int from, ConsensusMessage msg)
        {
            if (msg.PayloadCase != ConsensusMessage.PayloadOneofCase.SignedHeaderMessage)
                throw new Exception($"{msg.PayloadCase} message routed to Signed Header request");
            MessageReceived(from, 0);
        }

        public override ConsensusMessage CreateConsensusMessage(IProtocolIdentifier protocolId, int _)
        {
            var id = protocolId as RootProtocolId ?? throw new Exception($"wrong protcolId {protocolId} for Signed Header request");
            var headerRequest = new RequestSignedHeaderMessage();
            return new ConsensusMessage
            {
                RequestConsensus = new RequestConsensusMessage
                {
                    RequestSignedHeader = headerRequest
                }
            };
        }
    }
}