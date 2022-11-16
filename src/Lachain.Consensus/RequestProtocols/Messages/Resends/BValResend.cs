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

        protected override void HandleReceivedMessage(int from, ConsensusMessage msg)
        {
            if (msg.PayloadCase != ConsensusMessage.PayloadOneofCase.Bval)
                throw new Exception($"{msg.PayloadCase} message routed to Bval Resend");
            MessageReceived(from, msg.Bval.Value ? 1 : 0, msg);
        }
    }
}