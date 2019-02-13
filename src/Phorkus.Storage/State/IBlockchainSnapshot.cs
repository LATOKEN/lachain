using Phorkus.Proto;

namespace Phorkus.Storage.State
{
    public interface IBlockchainSnapshot
    {
        IBalanceSnapshot Balances { get; }
        IAssetSnapshot Assets { get; }
        IContractSnapshot Contracts { get; }
        IStorageSnapshot Storage { get; }
        ITransactionSnapshot Transactions { get; }
        IBlockSnapshot Blocks { get; }
        IWithdrawalSnapshot Withdrawals { get; }

        UInt256 StateHash { get; }
    }
}