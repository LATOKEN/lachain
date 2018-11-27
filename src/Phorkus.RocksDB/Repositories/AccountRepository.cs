using System;
using Phorkus.Proto;
using Phorkus.Core.Storage;
using Phorkus.Core.Storage.Repositories;

namespace Phorkus.RocksDB.Repositories
{
    public class AccountRepository : IAccountRepository
    {
        private readonly IRocksDbContext _rocksDbContext;
        
        public AccountRepository(IRocksDbContext rocksDbContext)
        {
            _rocksDbContext = rocksDbContext;
        }
        
        public Account GetAccountByAddress(UInt160 address)
        {
            throw new NotImplementedException();
        }
        
        public Account GetAccountByAddressOrDefault(UInt160 address)
        {
//            var raw = _rocksDbContext.Get(address.BuildStateAccountKey());
//            if (raw != null)
//                return Account.Parser.ParseFrom(raw);
//            var defaultValue = new Account
//            {
//                Address = address.ToArray()
//            };
//            return defaultValue;
            throw new NotImplementedException();
        }

        public void ChangeBalance(UInt160 address, UInt160 asset, UInt256 delta)
        {
            throw new NotImplementedException();
        }

        public void DeleteAccountByAddress(UInt160 address)
        {
            throw new NotImplementedException();
        }

        public Account AddAccount(Account account)
        {
//            if (account.Address is null)
//                throw new ArgumentException(nameof(account.Address));
//            _rocksDbContext.Save(account.Address.BuildStateAccountKey(), _binarySerializer.Serialize(account));
//            return account;
            throw new NotImplementedException();
        }
    }
}