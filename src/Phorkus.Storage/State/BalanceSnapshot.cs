using System.Runtime.CompilerServices;
using Google.Protobuf;
using Phorkus.Proto;
using Phorkus.Utility;
using Phorkus.Utility.Utils;

namespace Phorkus.Storage.State
{
    public class BalanceSnapshot : IBalanceSnapshot
    {
        private readonly IStorageState _state;

        public BalanceSnapshot(IStorageState state)
        {
            _state = state;
        }

        public ulong Version => _state.CurrentVersion;

        [MethodImpl(MethodImplOptions.Synchronized)]
        public Money GetAvailableBalance(UInt160 owner, UInt160 asset)
        {
            var key = EntryPrefix.BalanceByOwnerAndAsset.BuildPrefix(owner, asset);
            var value = _state.Get(key);
            var balance = value != null ? UInt256.Parser.ParseFrom(value): UInt256Utils.Zero;
            return new Money(balance);
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void SetAvailableBalance(UInt160 owner, UInt160 asset, Money value)
        {
            var key = EntryPrefix.BalanceByOwnerAndAsset.BuildPrefix(owner, asset);
            _state.AddOrUpdate(key, value.ToUInt256().ToByteArray());
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public Money AddAvailableBalance(UInt160 owner, UInt160 asset, Money value)
        {
            var balance = GetAvailableBalance(owner, asset);
            balance += value;
            SetAvailableBalance(owner, asset, balance);
            return balance;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public Money SubAvailableBalance(UInt160 owner, UInt160 asset, Money value)
        {
            var balance = GetAvailableBalance(owner, asset);
            balance -= value;
            SetAvailableBalance(owner, asset, balance);
            return balance;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public Money GetWithdrawingBalance(UInt160 owner, UInt160 asset)
        {
            var key = EntryPrefix.WithdrawingBalanceByOwnerAndAsset.BuildPrefix(owner, asset);
            var value = _state.Get(key);
            var balance = value != null ? UInt256.Parser.ParseFrom(value): UInt256Utils.Zero;
            return new Money(balance);
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void SetWithdrawingBalance(UInt160 owner, UInt160 asset, Money value)
        {
            var key = EntryPrefix.WithdrawingBalanceByOwnerAndAsset.BuildPrefix(owner, asset);
            _state.AddOrUpdate(key, value.ToUInt256().ToByteArray());
        }
        
        [MethodImpl(MethodImplOptions.Synchronized)]
        public Money AddWithdrawingBalance(UInt160 owner, UInt160 asset, Money value)
        {
            var balance = GetWithdrawingBalance(owner, asset);
            balance += value;
            SetWithdrawingBalance(owner, asset, balance);
            return balance;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public Money SubWithdrawingBalance(UInt160 owner, UInt160 asset, Money value)
        {
            var balance = GetWithdrawingBalance(owner, asset);
            balance -= value;
            SetWithdrawingBalance(owner, asset, balance);
            return balance;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public bool TransferAvailableBalance(UInt160 from, UInt160 to, UInt160 asset, Money value)
        {
            var availableBalance = GetAvailableBalance(from, asset);
            if (availableBalance.CompareTo(value) < 0)
                return false;
            SubAvailableBalance(from, asset, value);
            AddAvailableBalance(to, asset, value);
            return true;
        }
        
        public void Commit()
        {
            _state.Commit();
        }
    }
}