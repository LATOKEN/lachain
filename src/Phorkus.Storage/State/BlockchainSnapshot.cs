namespace Phorkus.Storage.State
{
    public class BlockchainSnapshot : IBlockchainSnapshot
    {
        public BlockchainSnapshot(IBalanceSnapshot balances, IAssetSnapshot assets, IContractSnapshot contracts)
        {
            Balances = balances;
            Assets = assets;
            Contracts = contracts;
        }

        public IBalanceSnapshot Balances { get; }
        public IAssetSnapshot Assets { get; }
        public IContractSnapshot Contracts { get; }
    }
}