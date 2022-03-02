using System.Runtime.CompilerServices;
using Lachain.Crypto;
using Lachain.Proto;
using Lachain.Utility;
using Lachain.Utility.Utils;
using Lachain.Storage.Trie;
using System.Collections.Generic;


namespace Lachain.Storage.State
{
    public class BalanceSnapshot : IBalanceSnapshot
    {
        private readonly IStorageState _state;
        private static readonly ICrypto Crypto = CryptoProvider.GetCrypto();

        public BalanceSnapshot(IStorageState state)
        {
            _state = state;
        }
        
        public IDictionary<ulong,IHashTrieNode> GetState()
        {
            return _state.GetAllNodes();
        }

        public bool IsTrieNodeHashesOk()
        {
            return _state.IsNodeHashesOk();
        }

        public ulong SetState(ulong root, IDictionary<ulong, IHashTrieNode> allTrieNodes)
        {
            return _state.InsertAllNodes(root, allTrieNodes);
        }

        public ulong Version => _state.CurrentVersion;

        public uint RepositoryId => _state.RepositoryId;

        [MethodImpl(MethodImplOptions.Synchronized)]
        public Money GetBalance(UInt160 owner)
        {
            var key = EntryPrefix.BalanceByOwnerAndAsset.BuildPrefix(owner);
            var value = _state.Get(key);
            var balance = value?.ToUInt256() ?? UInt256Utils.Zero;
            return new Money(balance);
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public Money GetSupply()
        {
            var key = EntryPrefix.TotalSupply.BuildPrefix();
            var value = _state.Get(key);
            var supply = value?.ToUInt256() ?? UInt256Utils.Zero;
            return new Money(supply);
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void SetBalance(UInt160 owner, Money value)
        {
            var key = EntryPrefix.BalanceByOwnerAndAsset.BuildPrefix(owner);
            _state.AddOrUpdate(key, value.ToUInt256().ToBytes());
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void SetSupply(Money value)
        {
            var key = EntryPrefix.TotalSupply.BuildPrefix();
            _state.AddOrUpdate(key, value.ToUInt256().ToBytes());
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public Money AddBalance(UInt160 owner, Money value, bool increaseSupply = false)
        {
            var balance = GetBalance(owner);
            balance += value;
            SetBalance(owner, balance);
            if (increaseSupply)
            {
                var supply = GetSupply();
                supply += value;
                SetSupply(supply);
            }

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
        
        [MethodImpl(MethodImplOptions.Synchronized)]
        public Money GetAllowedSupply()
        {
            var key = EntryPrefix.AllowedSupply.BuildPrefix();
            var value = _state.Get(key);
            var supply = value?.ToUInt256() ?? UInt256Utils.Zero;
            return new Money(supply);
        }
        
        [MethodImpl(MethodImplOptions.Synchronized)]
        public void SetAllowedSupply(Money value)
        {
            var key = EntryPrefix.AllowedSupply.BuildPrefix();
            _state.AddOrUpdate(key, value.ToUInt256().ToBytes());
        }
        
        [MethodImpl(MethodImplOptions.Synchronized)]
        public UInt160 GetMinter()
        {
            var key = EntryPrefix.MinterAddress.BuildPrefix();
            var value = _state.Get(key);
        
            if (value == null)
                return UInt160Utils.Zero;
            
            var address = value?.ToUInt160() ?? UInt160Utils.Zero;
            return address;
        }
        
        [MethodImpl(MethodImplOptions.Synchronized)]
        public void SetMinter(UInt160 value)
        {
            var key = EntryPrefix.MinterAddress.BuildPrefix();
            _state.AddOrUpdate(key, value.ToBytes());
        }

        public void Commit(RocksDbAtomicWrite batch)
        {
            _state.Commit(batch);
        }
        public void SetCurrentVersion(ulong root)
        {
            _state.SetCurrentVersion(root);
        }
        public void ClearCache()
        {
            _state.ClearCache();
        }
        public UInt256 Hash => _state.Hash;

        public ulong UpdateNodeIdToBatch(bool save)
        {
            return _state.UpdateNodeIdToBatch(save);
        }

        public ulong DeleteSnapshot(ulong block)
        {
            return _state.DeleteNodes(block);
        }
    }
}