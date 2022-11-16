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

        public override void HandleReceivedMessage(int from, ConsensusMessage msg)
        {
            if (msg.PayloadCase != ConsensusMessage.PayloadOneofCase.Conf)
                throw new Exception($"{msg.PayloadCase} message routed to Conf Resend");
            MessageReceived(from, 0, msg);
        }
    }
}