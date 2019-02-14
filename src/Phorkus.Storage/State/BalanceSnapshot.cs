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
        public Money GetAvailableBalance(UInt160 owner)
        {
            var key = EntryPrefix.BalanceByOwnerAndAsset.BuildPrefix(owner);
            var value = _state.Get(key);
            var balance = value != null ? UInt256.Parser.ParseFrom(value): UInt256Utils.Zero;
            return new Money(balance);
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void SetAvailableBalance(UInt160 owner, Money value)
        {
            var key = EntryPrefix.BalanceByOwnerAndAsset.BuildPrefix(owner);
            _state.AddOrUpdate(key, value.ToUInt256().ToByteArray());
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public Money AddAvailableBalance(UInt160 owner, Money value)
        {
            var balance = GetAvailableBalance(owner);
            balance += value;
            SetAvailableBalance(owner, balance);
            return balance;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public Money SubAvailableBalance(UInt160 owner, Money value)
        {
            var balance = GetAvailableBalance(owner);
            balance -= value;
            SetAvailableBalance(owner, balance);
            return balance;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public bool TransferAvailableBalance(UInt160 from, UInt160 to, Money value)
        {
            var availableBalance = GetAvailableBalance(from);
            if (availableBalance.CompareTo(value) < 0)
                return false;
            SubAvailableBalance(from, value);
            AddAvailableBalance(to, value);
            return true;
        }
        
        public void Commit()
        {
            _state.Commit();
        }

        public UInt256 Hash => _state.Hash;
    }
}