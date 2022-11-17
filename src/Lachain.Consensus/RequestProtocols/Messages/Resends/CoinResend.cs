using System;
using Lachain.Proto;

namespace Lachain.Consensus.RequestProtocols.Messages.Resends
{
    public class CoinResend : MessageResendHandler
    {
        public CoinResend(RequestType type, int validatorCount, int msgPerValidator)
            : base(type, validatorCount, msgPerValidator)
        {

        }

        protected override void HandleSentMessage(int validator, ConsensusMessage msg)
        {
            if (msg.PayloadCase != ConsensusMessage.PayloadOneofCase.Coin)
                throw new Exception($"{msg.PayloadCase} message routed to Coin Resend");
            SaveMessage(validator, 0, msg);
        }
    }
}