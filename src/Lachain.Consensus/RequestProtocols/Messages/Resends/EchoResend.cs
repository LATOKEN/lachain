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

        protected override void HandleSentMessage(int validator, ConsensusMessage msg)
        {
            if (msg.PayloadCase != ConsensusMessage.PayloadOneofCase.EchoMessage)
                throw new Exception($"{msg.PayloadCase} message routed to Echo Resend");
            SaveMessage(validator, 0, msg);
        }
    }
}