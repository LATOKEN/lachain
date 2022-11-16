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

        public override void HandleReceivedMessage(int from, ConsensusMessage msg)
        {
            if (msg.PayloadCase != ConsensusMessage.PayloadOneofCase.ValMessage)
                throw new Exception($"{msg.PayloadCase} message routed to Val Resend");
            MessageReceived(from, 0, msg);
        }
    }
}