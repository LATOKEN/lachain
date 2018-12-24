using Google.Protobuf;
using Phorkus.Core.Blockchain.State;
using Phorkus.Hestia.Repositories;
using Phorkus.Proto;
using Phorkus.RocksDB;
using Phorkus.Utility;
using Phorkus.Utility.Utils;

namespace Phorkus.Hestia.State
{
    public class BalanceSnapshot : IBalanceSnapshot, ISnapshot
    {
        private readonly IStorageState _state;

        internal BalanceSnapshot(IStorageState state)
        {
            _state = state;
        }

        public ulong Version => _state.CurrentVersion;

        public Money GetAvailableBalance(UInt160 owner, UInt160 asset)
        {
            var key = EntryPrefix.BalanceByOwnerAndAsset.BuildPrefix(owner, asset);
            var value = _state.Get(key);
            var balance = value != null ? UInt256.Parser.ParseFrom(value): UInt256Utils.Zero;
            return new Money(balance);
        }

        public void SetAvailableBalance(UInt160 owner, UInt160 asset, Money value)
        {
            var key = EntryPrefix.BalanceByOwnerAndAsset.BuildPrefix(owner, asset);
            _state.AddOrUpdate(key, value.ToUInt256().ToByteArray());
        }

        public Money AddAvailableBalance(UInt160 owner, UInt160 asset, Money value)
        {
            var balance = GetAvailableBalance(owner, asset);
            balance += value;
            SetAvailableBalance(owner, asset, balance);
            return balance;
        }

        public Money SubAvailableBalance(UInt160 owner, UInt160 asset, Money value)
        {
            var balance = GetAvailableBalance(owner, asset);
            balance -= value;
            SetAvailableBalance(owner, asset, balance);
            return balance;
        }

        public Money GetWithdrawingBalance(UInt160 owner, UInt160 asset)
        {
            var key = EntryPrefix.WithdrawingBalanceByOwnerAndAsset.BuildPrefix(owner, asset);
            var value = _state.Get(key);
            var balance = value != null ? UInt256.Parser.ParseFrom(value): UInt256Utils.Zero;
            return new Money(balance);
        }

        public void SetWithdrawingBalance(UInt160 owner, UInt160 asset, Money value)
        {
            var key = EntryPrefix.WithdrawingBalanceByOwnerAndAsset.BuildPrefix(owner, asset);
            _state.AddOrUpdate(key, value.ToUInt256().ToByteArray());
        }

        public Money AddWithdrawingBalance(UInt160 owner, UInt160 asset, Money value)
        {
            var balance = GetWithdrawingBalance(owner, asset);
            balance += value;
            SetWithdrawingBalance(owner, asset, balance);
            return balance;
        }

        public Money SubWithdrawingBalance(UInt160 owner, UInt160 asset, Money value)
        {
            var balance = GetWithdrawingBalance(owner, asset);
            balance -= value;
            SetWithdrawingBalance(owner, asset, balance);
            return balance;
        }

        public void TransferAvailableBalance(UInt160 from, UInt160 to, UInt160 asset, Money value)
        {
            SubAvailableBalance(from, asset, value);
            AddAvailableBalance(to, asset, value);
        }
        
        public void Commit()
        {
            _state.Commit();
        }
    }
}