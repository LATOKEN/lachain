using System;

namespace Phorkus.Consensus.CommonCoin
{
    public interface ICommonCoin : IConsensusMessageHandler
    {
        void RequestCoin();
        event EventHandler<bool> CoinTossed;
    }
}