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

        public override void HandleReceivedMessage(int from, ConsensusMessage msg)
        {
            if (msg.PayloadCase != ConsensusMessage.PayloadOneofCase.SignedHeaderMessage)
                throw new Exception($"{msg.PayloadCase} message routed to Signed Header Resend");
            MessageReceived(from, 0, msg);
        }
    }
}