using Lachain.Proto;

namespace Lachain.Storage.State
{
    public interface IBlockchainSnapshot
    {
        IBalanceSnapshot Balances { get; }
        IContractSnapshot Contracts { get; }
        IStorageSnapshot Storage { get; }
        ITransactionSnapshot Transactions { get; }
        IBlockSnapshot Blocks { get; }
        IEventSnapshot Events { get; }
        IValidatorSnapshot Validators { get; }

        UInt256 StateHash { get; }
    }
}