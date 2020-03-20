using System.Linq;
using Lachain.Crypto;
using Lachain.Proto;
using Lachain.Utility.Utils;

namespace Lachain.Storage.State
{
    public class BlockchainSnapshot : IBlockchainSnapshot
    {
        public BlockchainSnapshot(
            IBalanceSnapshot balances,
            IContractSnapshot contracts,
            IStorageSnapshot storage,
            ITransactionSnapshot transactions,
            IBlockSnapshot blocks,
            IEventSnapshot events,
            IValidatorSnapshot validators
        )
        {
            Balances = balances;
            Contracts = contracts;
            Storage = storage;
            Transactions = transactions;
            Blocks = blocks;
            Events = events;
            Validators = validators;
        }

        public IBalanceSnapshot Balances { get; }
        public IContractSnapshot Contracts { get; }
        public IStorageSnapshot Storage { get; }
        public ITransactionSnapshot Transactions { get; }
        public IBlockSnapshot Blocks { get; }
        public IEventSnapshot Events { get; }
        public IValidatorSnapshot Validators { get; }

        public UInt256 StateHash
        {
            get
            {
                return new ISnapshot[] {Balances, Contracts, Storage, Transactions, Events, Validators}
                    .Aggregate(
                        Enumerable.Empty<byte>(),
                        (current, snapshot) => current.Concat(snapshot.Hash.Buffer.ToByteArray()))
                    .Keccak256().ToUInt256();
            }
        }
    }
}