using System;
using Lachain.Proto;

namespace Lachain.Consensus.RequestProtocols.Messages.Resends
{
    public class BValResend : MessageResendHandler
    {
        public BValResend(RequestType type, int validatorCount, int msgPerValidator)
            : base(type, validatorCount, msgPerValidator)
        {

        }

        protected override void HandleSentMessage(int validator, ConsensusMessage msg)
        {
            if (msg.PayloadCase != ConsensusMessage.PayloadOneofCase.Bval)
                throw new Exception($"{msg.PayloadCase} message routed to Bval Resend");
            SaveMessage(validator, msg.Bval.Value ? 1 : 0, msg);
        }
    }
}