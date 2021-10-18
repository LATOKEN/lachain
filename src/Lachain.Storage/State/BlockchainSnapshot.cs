using System.Linq;
using System.Numerics;
using Lachain.Crypto;
using Lachain.Proto;
using Lachain.Utility.Utils;
using Lachain.Logger;

namespace Lachain.Storage.State
{
    public class BlockchainSnapshot : IBlockchainSnapshot
    {

        private static readonly ILogger<BlockchainSnapshot> Logger =
            LoggerFactory.GetLoggerForClass<BlockchainSnapshot>();

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

        public BigInteger NetworkGasPrice
        {
            get {
                var stakers = Storage.GetRawValue(new BigInteger(3).ToUInt160(), new BigInteger(6).ToUInt256().Buffer);
                var networkSize = stakers.Length / CryptoUtils.PublicKeyLength - 1;
                if (networkSize == 0) networkSize = 1;
                
                var basicGasPrice = Storage.GetRawValue(new BigInteger(2).ToUInt160(), new BigInteger(8).ToUInt256().Buffer).ToUInt256().ToBigInteger();
                return basicGasPrice / networkSize;
            }
        }

        public ISnapshot? GetSnapshot(string snapshotName)
        {
            string[] snapshotNames = new string[] { "Balances", "Contracts", "Storage", "Transactions", "Blocks", "Events", "Validators" };
            ISnapshot[] snapshots = new ISnapshot[]{Balances, Contracts, Storage, Transactions, Blocks, Events, Validators};
            for(int i = 0; i < snapshotNames.Length; i++)
            {
                if(snapshotNames[i] == snapshotName) return snapshots[i];
            }
            return null;
        }

        public ISnapshot[] GetAllSnapshot()
        {
            return new ISnapshot[]{Balances, Contracts, Storage, Transactions, Blocks, Events, Validators};
        }

        public UInt256 StateHash
        {
            get
            {
                string[] snapshotNames = new string[] { "Balances", "Contracts", "Storage", "Transactions", "Blocks", "Events", "Validators" };
                ISnapshot[] snapshots = new ISnapshot[]{Balances, Contracts, Storage, Transactions, Blocks, Events, Validators};

                for(int i = 0; i < snapshotNames.Length; i++)
                {
                    Logger.LogDebug($"{snapshotNames[i]}'s hash: {snapshots[i].Hash.ToHex()}");
                }

                return new ISnapshot[] {Balances, Contracts, Storage, Transactions, Events, Validators}
                    .Select(snapshot => snapshot.Hash.ToBytes())
                    .Flatten()
                    .Keccak();
            }
        }
    }
}