using Phorkus.Consensus.CommonCoin;

namespace Phorkus.Consensus.Messages
{
    public class CoinTossed : InternalMessage
    {
        public bool CoinValue { get; }
        public CoinId CoinId { get; }

        public CoinTossed(CoinId coinId, bool value)
        {
            CoinValue = value;
            CoinId = coinId;
        }
    }
}