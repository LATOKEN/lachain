namespace Phorkus.Storage.State
{
    public interface IBlockchainSnapshot
    {
        IBalanceSnapshot Balances { get; }
        IAssetSnapshot Assets { get; }
        IContractSnapshot Contracts { get; }
    }
}