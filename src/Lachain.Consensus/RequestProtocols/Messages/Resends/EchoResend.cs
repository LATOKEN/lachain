using System;
using Lachain.Proto;

namespace Lachain.Consensus.RequestProtocols.Messages.Resends
{
    public class EchoResend : MessageResendHandler
    {
        public EchoResend(RequestType type, int validatorCount, int msgPerValidator)
            : base(type, validatorCount, msgPerValidator)
        {

        }

        public override void HandleReceivedMessage(int from, ConsensusMessage msg)
        {
            if (msg.PayloadCase != ConsensusMessage.PayloadOneofCase.EchoMessage)
                throw new Exception($"{msg.PayloadCase} message routed to Echo Resend");
            MessageReceived(from, 0, msg);
        }
    }
}