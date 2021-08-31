using System.Numerics;
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
        
        public ISnapshot? GetSnapshot(string snapshotName);

        public ISnapshot[] GetAllSnapshot();

        BigInteger NetworkGasPrice { get; }

        UInt256 StateHash { get; }
    }
}