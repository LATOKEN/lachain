using System;
using System.Threading.Tasks;
using NeoSharp.BinarySerialization;
using NeoSharp.Core.Models;
using NeoSharp.Core.Storage.State;
using NeoSharp.Types;

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
        
        public async Task<Account> GetAccountByAddress(UInt160 address)
        {
            var raw = await _rocksDbContext.Get(address.BuildStateAccountKey());
            return raw == null ? null : _binarySerializer.Deserialize<Account>(raw);
        }

        public async Task<Account> GetAccountByAddressOrDefault(UInt160 address)
        {
            var raw = await _rocksDbContext.Get(address.BuildStateAccountKey());
            return raw != null ? _binarySerializer.Deserialize<Account>(raw) : new Account(address);
        }

        public async Task DeleteAccountByAddress(UInt160 address)
        {
            await _rocksDbContext.Delete(address.BuildStateAccountKey());
        }

        public async Task<Account> AddAccount(Account account)
        {
            if (account.Address is null)
                throw new ArgumentException(nameof(account.Address));
            await _rocksDbContext.Save(account.Address.BuildStateAccountKey(), _binarySerializer.Serialize(account));
            return account;
        }
    }
}