using System;
using Lachain.Proto;

namespace Lachain.Consensus.RequestProtocols.Messages.Resends
{
    public class ConfResend : MessageResendHandler
    {
        public ConfResend(RequestType type, int validatorCount, int msgPerValidator)
            : base(type, validatorCount, msgPerValidator)
        {

        }

        protected override void HandleSentMessage(int validator, ConsensusMessage msg)
        {
            if (msg.PayloadCase != ConsensusMessage.PayloadOneofCase.Conf)
                throw new Exception($"{msg.PayloadCase} message routed to Conf Resend");
            SaveMessage(validator, 0, msg);
        }
    }
}