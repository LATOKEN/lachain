namespace Phorkus.Consensus.CommonCoin
{
    public interface ICommonCoin : IConsensusProtocol
    {
        void RequestCoin();
        bool Terminated(out bool coin);
    }
}