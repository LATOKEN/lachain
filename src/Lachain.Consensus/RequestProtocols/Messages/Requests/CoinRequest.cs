using System;
using Lachain.Consensus.CommonCoin;
using Lachain.Proto;

namespace Lachain.Consensus.RequestProtocols.Messages.Requests
{
    public class CoinRequest : MessageRequestHandler
    {
        public CoinRequest(RequestType type, int validatorCount, int msgPerValidator)
            : base(type, validatorCount, msgPerValidator)
        {

        }

        protected override void HandleReceivedMessage(int from, ConsensusMessage msg)
        {
            if (msg.PayloadCase != ConsensusMessage.PayloadOneofCase.Coin)
                throw new Exception($"{msg.PayloadCase} message routed to Coin request");
            MessageReceived(from, 0);
        }

        protected override ConsensusMessage CreateConsensusMessage(IProtocolIdentifier protocolId, int _)
        {
            var id = protocolId as CoinId ?? throw new Exception($"wrong protcolId {protocolId} for Coin request");
            var coinRequest = new RequestCommonCoinMessage
            {
                Agreement = (int) id.Agreement,
                Epoch = (int) id.Epoch
            };
            return new ConsensusMessage
            {
                RequestConsensus = new RequestConsensusMessage
                {
                    RequestCoin = coinRequest
                }
            };
        }
    }
}