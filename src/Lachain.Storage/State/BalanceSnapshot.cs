using System.Runtime.CompilerServices;
using Google.Protobuf;
using Lachain.Proto;
using Lachain.Utility;
using Lachain.Utility.Utils;

namespace Lachain.Storage.State
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
        public Money GetBalance(UInt160 owner)
        {
            var key = EntryPrefix.BalanceByOwnerAndAsset.BuildPrefix(owner);
            var value = _state.Get(key);
            var balance = value != null ? UInt256.Parser.ParseFrom(value): UInt256Utils.Zero;
            return new Money(balance);
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void SetBalance(UInt160 owner, Money value)
        {
            var key = EntryPrefix.BalanceByOwnerAndAsset.BuildPrefix(owner);
            _state.AddOrUpdate(key, value.ToUInt256(false).ToByteArray());
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public Money AddBalance(UInt160 owner, Money value)
        {
            var balance = GetBalance(owner);
            balance += value;
            SetBalance(owner, balance);
            return balance;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public Money SubBalance(UInt160 owner, Money value)
        {
            var balance = GetBalance(owner);
            balance -= value;
            SetBalance(owner, balance);
            return balance;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public bool TransferBalance(UInt160 from, UInt160 to, Money value)
        {
            var availableBalance = GetBalance(from);
            if (availableBalance.CompareTo(value) < 0)
                return false;
            SubBalance(from, value);
            AddBalance(to, value);
            return true;
        }
        
        public void Commit()
        {
            _state.Commit();
        }

        public UInt256 Hash => _state.Hash;
    }
}