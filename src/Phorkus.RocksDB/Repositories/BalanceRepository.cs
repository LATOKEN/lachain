using Google.Protobuf;
using Phorkus.Core.Blockchain;
using Phorkus.Proto;
using Phorkus.Core.Storage;
using Phorkus.Utility;

namespace Phorkus.RocksDB.Repositories
{
    public class BalanceRepository : IBalanceRepository
    {
        private readonly IRocksDbContext _rocksDbContext;

        public BalanceRepository(IRocksDbContext rocksDbContext)
        {
            _rocksDbContext = rocksDbContext;
        }

        public Money GetBalance(UInt160 owner, UInt160 asset)
        {
            lock (_rocksDbContext)
            {
                return _GetBalanceOrDefaultUnsafe(owner, asset);
            }
        }

        public Money AddBalance(UInt160 owner, UInt160 asset, Money value)
        {
            lock (_rocksDbContext)
            {
                var balance = _GetBalanceOrDefaultUnsafe(owner, asset);
                var result = balance + value;
                if (result < balance + value)
                    throw new InsufficientFundsException();
                return _ChangeBalanceUnsafe(owner, asset, result);
            }
        }

        public void TransferBalance(UInt160 from, UInt160 to, UInt160 asset, Money value)
        {
            lock (_rocksDbContext)
            {
                /* TODO: "this code might be unsafe" */
                SubBalance(from, asset, value);
                AddBalance(to, asset, value);
            }
        }

        public Money SubBalance(UInt160 owner, UInt160 asset, Money value)
        {
            lock (_rocksDbContext)
            {
                var balance = _GetBalanceOrDefaultUnsafe(owner, asset);
                var result = balance - value;
                if (result < Money.Zero)
                    throw new InsufficientFundsException();
                return _ChangeBalanceUnsafe(owner, asset, result);
            }
        }

        private Money _GetBalanceOrDefaultUnsafe(UInt160 owner, UInt160 asset)
        {
            var raw = _rocksDbContext.Get(EntryPrefix.BalanceByOwnerAndAsset.BuildPrefix(owner, asset));
            if (raw == null)
                return Money.Zero;
            var balance = Balance.Parser.ParseFrom(raw);
            return new Money(balance.Amount);
        }

        private Money _ChangeBalanceUnsafe(UInt160 owner, UInt160 asset, Money amount)
        {
            var prefix = EntryPrefix.BalanceByOwnerAndAsset.BuildPrefix(owner, asset);
            var raw = _rocksDbContext.Get(prefix);
            var balance = raw != null
                ? Balance.Parser.ParseFrom(raw)
                : new Balance
                {
                    Address = owner,
                    Asset = asset
                };
            balance.Amount = amount.ToUInt256();
            _rocksDbContext.Save(prefix, balance.ToByteArray());
            return amount;
        }
    }
}