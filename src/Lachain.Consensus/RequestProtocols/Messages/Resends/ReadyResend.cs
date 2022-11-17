using System;
using Lachain.Proto;

namespace Lachain.Consensus.RequestProtocols.Messages.Resends
{
    public class ReadyResend : MessageResendHandler
    {
        public ReadyResend(RequestType type, int validatorCount, int msgPerValidator)
            : base(type, validatorCount, msgPerValidator)
        {

        }

        protected override void HandleSentMessage(int validator, ConsensusMessage msg)
        {
            if (msg.PayloadCase != ConsensusMessage.PayloadOneofCase.ReadyMessage)
                throw new Exception($"{msg.PayloadCase} message routed to Ready resend");
            SaveMessage(validator, 0, msg);
        }
    }
}