namespace Phorkus.Core.Blockchain.State
{
    public interface IBlockchainSnapshot
    {
        IBalanceSnapshot Balances { get; }
    }
}