using System;
using Lachain.Proto;

namespace Lachain.Consensus.RequestProtocols.Messages.Resends
{
    public class DecryptedResend : MessageResendHandler
    {
        public DecryptedResend(RequestType type, int validatorCount, int msgPerValidator)
            : base(type, validatorCount, msgPerValidator)
        {

        }

        protected override void HandleSentMessage(int validator, ConsensusMessage msg)
        {
            if (msg.PayloadCase != ConsensusMessage.PayloadOneofCase.Decrypted)
                throw new Exception($"{msg.PayloadCase} message routed to Decrypted request");
            SaveMessage(validator, msg.Decrypted.ShareId, msg);
        }
    }
}