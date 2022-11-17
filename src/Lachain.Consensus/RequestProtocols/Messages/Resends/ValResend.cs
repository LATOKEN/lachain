using System;
using Lachain.Proto;

namespace Lachain.Consensus.RequestProtocols.Messages.Resends
{
    public class ValResend : MessageResendHandler
    {
        public ValResend(RequestType type, int validatorCount, int msgPerValidator)
            : base(type, validatorCount, msgPerValidator)
        {

        }

        protected override void HandleSentMessage(int validator, ConsensusMessage msg)
        {
            if (msg.PayloadCase != ConsensusMessage.PayloadOneofCase.ValMessage)
                throw new Exception($"{msg.PayloadCase} message routed to Val Resend");
            SaveMessage(validator, 0, msg);
        }
    }
}