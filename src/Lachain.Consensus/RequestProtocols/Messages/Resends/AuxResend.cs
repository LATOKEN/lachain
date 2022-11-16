using System;
using Lachain.Proto;

namespace Lachain.Consensus.RequestProtocols.Messages.Resends
{
    public class AuxResend : MessageResendHandler
    {
        public AuxResend(RequestType type, int validatorCount, int msgPerValidator)
            : base(type, validatorCount, msgPerValidator)
        {

        }

        protected override void HandleReceivedMessage(int from, ConsensusMessage msg)
        {
            if (msg.PayloadCase != ConsensusMessage.PayloadOneofCase.Aux)
                throw new Exception($"{msg.PayloadCase} message routed to Aux Resend");
            MessageReceived(from, 0, msg);
        }
    }
}