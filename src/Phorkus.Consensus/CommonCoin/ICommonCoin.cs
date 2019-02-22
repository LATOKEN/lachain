using System;

namespace Phorkus.Consensus.CommonCoin
{
    public interface ICommonCoin : IConsensusProtocol
    {
        void RequestCoin();
        event EventHandler<bool> CoinTossed;
    }
}