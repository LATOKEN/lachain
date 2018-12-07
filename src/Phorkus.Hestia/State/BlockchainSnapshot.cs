using Phorkus.Core.Blockchain.State;

namespace Phorkus.Hestia.State
{
    class BlockchainSnapshot : IBlockchainSnapshot
    {
        public BlockchainSnapshot(IBalanceSnapshot balances, IAssetSnapshot assets)
        {
            Balances = balances;
            Assets = assets;
        }

        public IBalanceSnapshot Balances { get; }
        public IAssetSnapshot Assets { get; }
    }
}