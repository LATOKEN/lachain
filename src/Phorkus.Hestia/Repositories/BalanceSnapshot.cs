using Google.Protobuf;
using Phorkus.Core.Blockchain.State;
using Phorkus.Proto;
using Phorkus.RocksDB;
using Phorkus.Utility;
using Phorkus.Utility.Utils;

namespace Phorkus.Hestia.Repositories
{
    public class BalanceSnapshot : IBalanceSnapshot
    {
        private readonly IStorageState _state;

        internal BalanceSnapshot(IStorageState state)
        {
            _state = state;
        }

        public ulong Version => _state.CurrentVersion;

        public Money GetBalance(UInt160 owner, UInt160 asset)
        {
            var key = EntryPrefix.BalanceByOwnerAndAsset.BuildPrefix(owner, asset);
            var value = _state.Get(key);
            var balance = value != null ? UInt256.Parser.ParseFrom(value): UInt256Utils.Zero;
            return new Money(balance);
        }

        public void TransferBalance(UInt160 from, UInt160 to, UInt160 asset, Money value)
        {
            SubBalance(from, asset, value);
            AddBalance(to, asset, value);
        }
        
        public Money AddBalance(UInt160 owner, UInt160 asset, Money value)
        {
            var balance = GetBalance(owner, asset);
            balance += value;
            ChangeBalance(owner, asset, balance);
            return balance;
        }

        public Money SubBalance(UInt160 owner, UInt160 asset, Money value)
        {
            var balance = GetBalance(owner, asset);
            balance -= value;
            ChangeBalance(owner, asset, balance);
            return balance;
        }

        public void Commit()
        {
            _state.Commit();
        }

        private void ChangeBalance(UInt160 owner, UInt160 asset, Money amount)
        {
            var key = EntryPrefix.BalanceByOwnerAndAsset.BuildPrefix(owner, asset);
            var value = amount.ToUInt256().ToByteArray();
            _state.AddOrUpdate(key, value);
        }
    }
}