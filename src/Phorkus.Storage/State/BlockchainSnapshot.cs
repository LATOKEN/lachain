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
            IContractSnapshot contracts,
            IStorageSnapshot storage,
            ITransactionSnapshot transactions,
            IBlockSnapshot blocks,
            IEventSnapshot events)
        {
            Balances = balances;
            Contracts = contracts;
            Storage = storage;
            Transactions = transactions;
            Blocks = blocks;
            Events = events;
        }

        public IBalanceSnapshot Balances { get; }
        public IContractSnapshot Contracts { get; }
        public IStorageSnapshot Storage { get; }
        public ITransactionSnapshot Transactions { get; }
        public IBlockSnapshot Blocks { get; }
        public IEventSnapshot Events { get; }

        public UInt256 StateHash
        {
            get
            {
                return new ISnapshot[] {Balances, Contracts, Storage, Transactions, Events}
                    .Aggregate(
                        Enumerable.Empty<byte>(),
                        (current, snapshot) => current.Concat(snapshot.Hash.Buffer.ToByteArray()))
                    .Keccak256().ToUInt256();
            }
        }
    }
}