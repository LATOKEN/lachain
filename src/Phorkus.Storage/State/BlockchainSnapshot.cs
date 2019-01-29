namespace Phorkus.Storage.State
{
    public class BlockchainSnapshot : IBlockchainSnapshot
    {
        public BlockchainSnapshot(
            IBalanceSnapshot balances, IAssetSnapshot assets, IContractSnapshot contracts,
            IContractStorageSnapshot contractStorage
        )
        {
            Balances = balances;
            Assets = assets;
            Contracts = contracts;
            ContractStorage = contractStorage;
        }

        public IBalanceSnapshot Balances { get; }
        public IAssetSnapshot Assets { get; }
        public IContractSnapshot Contracts { get; }
        public IContractStorageSnapshot ContractStorage { get; }
    }
}