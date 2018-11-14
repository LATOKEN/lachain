using System;
using NeoSharp.BinarySerialization;
using NeoSharp.Core;
using NeoSharp.Core.Models;
using NeoSharp.Core.Storage.State;

namespace NeoSharp.RocksDB.Repositories
{
    public class AccountRepository : IAccountRepository
    {
        private readonly IBinarySerializer _binarySerializer;
        private readonly IRocksDbContext _rocksDbContext;
        
        public AccountRepository(
            IRocksDbContext rocksDbContext,
            IBinarySerializer binarySerializer)
        {
            _rocksDbContext = rocksDbContext;
            _binarySerializer = binarySerializer;
        }
        
        public Account GetAccountByAddress(UInt160 address)
        {
            var raw = _rocksDbContext.Get(address.BuildStateAccountKey());
            return raw == null ? null : _binarySerializer.Deserialize<Account>(raw);
        }

        public Account GetAccountByAddressOrDefault(UInt160 address)
        {
            var raw = _rocksDbContext.Get(address.BuildStateAccountKey());
            return raw != null ? _binarySerializer.Deserialize<Account>(raw) : new Account(address);
        }

        public void ChangeBalance(UInt160 address, UInt160 asset, UInt256 delta)
        {
            throw new NotImplementedException();
        }

        public void DeleteAccountByAddress(UInt160 address)
        {
            _rocksDbContext.Delete(address.BuildStateAccountKey());
        }

        public Account AddAccount(Account account)
        {
            if (account.Address is null)
                throw new ArgumentException(nameof(account.Address));
            _rocksDbContext.Save(account.Address.BuildStateAccountKey(), _binarySerializer.Serialize(account));
            return account;
        }
    }
}