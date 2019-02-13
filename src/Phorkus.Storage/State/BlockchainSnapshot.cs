using System.Linq;
using Phorkus.Crypto;
using Phorkus.Proto;
using Phorkus.Utility.Utils;

namespace Phorkus.Storage.State
{
    public class BlockchainSnapshot : IBlockchainSnapshot
    {
        public BlockchainSnapshot(
            IBalanceSnapshot balances,
            IAssetSnapshot assets,
            IContractSnapshot contracts,
            IStorageSnapshot storage,
            ITransactionSnapshot transactions,
            IBlockSnapshot blocks,
            IWithdrawalSnapshot withdrawals)
        {
            Balances = balances;
            Assets = assets;
            Contracts = contracts;
            Storage = storage;
            Transactions = transactions;
            Blocks = blocks;
            Withdrawals = withdrawals;
        }

        public IBalanceSnapshot Balances { get; }
        public IAssetSnapshot Assets { get; }
        public IContractSnapshot Contracts { get; }
        public IStorageSnapshot Storage { get; }
        public ITransactionSnapshot Transactions { get; }
        public IBlockSnapshot Blocks { get; }
        public IWithdrawalSnapshot Withdrawals { get; }

        public UInt256 StateHash
        {
            get
            {
                return new ISnapshot[] {Balances, Assets, Contracts, Storage, Transactions, Withdrawals}
                    .Aggregate(
                        Enumerable.Empty<byte>(),
                        (current, snapshot) => current.Concat(snapshot.Hash.Buffer.ToByteArray()))
                    .Keccak256().ToUInt256();
            }
        }
    }
}