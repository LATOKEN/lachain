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

        protected override void HandleReceivedMessage(int from, ConsensusMessage msg)
        {
            if (msg.PayloadCase != ConsensusMessage.PayloadOneofCase.Coin)
                throw new Exception($"{msg.PayloadCase} message routed to Coin Resend");
            MessageReceived(from, 0, msg);
        }
    }
}