using Phorkus.Core.Blockchain.State;

namespace Phorkus.Hestia.State
{
    class BlockchainSnapshot : IBlockchainSnapshot
    {
        public BlockchainSnapshot(
            IBalanceSnapshot balances
        )
        {
            Balances = balances;
        }

        public IBalanceSnapshot Balances { get; }
    }
}