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
        private static readonly ILogger<BlockchainSnapshot> Logger = LoggerFactory.GetLoggerForClass<BlockchainSnapshot>();
        
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
        public IBlockSnapshot Blocks { get; set; }
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

        public UInt256 StateHash
        {
            get
            {
                return new ISnapshot[] {Balances, Contracts, Storage, Transactions, Events, Validators}
                    .Select(snapshot => snapshot.Hash.ToBytes())
                    .Flatten()
                    .Keccak();
            }
        }

        public void GetStateHash()
        {
            Logger.LogInformation($"== Balance = {Balances.Hash.ToBytes().Keccak()}");
            Logger.LogInformation($"== Contracts = {Contracts.Hash.ToBytes().Keccak()}");
            Logger.LogInformation($"== Storage = {Storage.Hash.ToBytes().Keccak()}");
            Logger.LogInformation($"== Transactions = {Transactions.Hash.ToBytes().Keccak()}");
            Logger.LogInformation($"== Events = {Events.Hash.ToBytes().Keccak()}");
            Logger.LogInformation($"== Validators = {Validators.Hash.ToBytes().Keccak()}");
            
            // Logger.LogInformation($"== Balance = {Balances.Version}");
            // Logger.LogInformation($"== Contracts = {Contracts.Version}");
            // Logger.LogInformation($"== Storage = {Storage.Version}");
            // Logger.LogInformation($"== Transactions = {Transactions.Version}");
            // Logger.LogInformation($"== Events = {Events.Version}");
            // Logger.LogInformation($"== Validators = {Validators.Version}");
        }
    }
}