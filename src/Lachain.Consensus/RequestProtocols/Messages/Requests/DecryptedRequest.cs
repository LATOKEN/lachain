using System;
using Lachain.Consensus.HoneyBadger;
using Lachain.Proto;

namespace Lachain.Consensus.RequestProtocols.Messages.Requests
{
    public class DecryptedRequest : MessageRequestHandler
    {
        public DecryptedRequest(RequestType type, int validatorCount, int msgPerValidator)
            : base(type, validatorCount, msgPerValidator)
        {

        }

        protected override void HandleReceivedMessage(int from, ConsensusMessage msg)
        {
            if (msg.PayloadCase != ConsensusMessage.PayloadOneofCase.Decrypted)
                throw new Exception($"{msg.PayloadCase} message routed to Decrypted request");
            MessageReceived(from, msg.Decrypted.ShareId);
        }

        protected override ConsensusMessage CreateConsensusRequestMessage(IProtocolIdentifier protocolId, int msgId)
        {
            var id = protocolId as HoneyBadgerId ?? throw new Exception($"wrong protcolId {protocolId} for Decrypted request");
            var decryptedRequest = new RequestTPKEPartiallyDecryptedShareMessage
            {
                ShareId = msgId
            };
            return new ConsensusMessage
            {
                RequestConsensus = new RequestConsensusMessage
                {
                    RequestDecrypted = decryptedRequest
                }
            };
        }

        public static HoneyBadgerId CreateProtocolId(RequestConsensusMessage msg, long era)
        {
            if (msg.PayloadCase != RequestConsensusMessage.PayloadOneofCase.RequestDecrypted)
                throw new Exception($"{msg.PayloadCase} routed to Decrypted Request");
            return new HoneyBadgerId(era);
        }
    }
}