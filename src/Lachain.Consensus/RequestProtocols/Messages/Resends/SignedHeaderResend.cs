using System;
using Lachain.Proto;

namespace Lachain.Consensus.RequestProtocols.Messages.Resends
{
    public class SignedHeaderResend : MessageResendHandler
    {
        public SignedHeaderResend(RequestType type, int validatorCount, int msgPerValidator)
            : base(type, validatorCount, msgPerValidator)
        {

        }

        protected override void HandleSentMessage(int validator, ConsensusMessage msg)
        {
            if (msg.PayloadCase != ConsensusMessage.PayloadOneofCase.SignedHeaderMessage)
                throw new Exception($"{msg.PayloadCase} message routed to Signed Header Resend");
            SaveMessage(validator, 0, msg);
        }
    }
}