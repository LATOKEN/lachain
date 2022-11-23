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

        public override ConsensusMessage CreateConsensusRequestMessage(IProtocolIdentifier protocolId, int _)
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

        public static CoinId CreateProtocolId(RequestConsensusMessage msg, long era)
        {
            if (msg.PayloadCase != RequestConsensusMessage.PayloadOneofCase.RequestCoin)
                throw new Exception($"{msg.PayloadCase} routed to Coin Request");
            return new CoinId(era, msg.RequestCoin.Agreement, msg.RequestCoin.Epoch);
        }
    }
}