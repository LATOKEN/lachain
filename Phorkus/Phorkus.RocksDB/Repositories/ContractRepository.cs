using System;
using Phorkus.Core.Proto;
using Phorkus.Core.Storage;

namespace Phorkus.RocksDB.Repositories
{
    public class ContractRepository : IContractRepository
    {
        private readonly IRocksDbContext _rocksDbContext;

        public ContractRepository(IRocksDbContext rocksDbContext)
        {
            _rocksDbContext = rocksDbContext ?? throw new ArgumentNullException(nameof(rocksDbContext));
        }

        public Contract GetContractByHash(UInt160 contractHash)
        {
            throw new NotImplementedException();
        }
        
        public void AddContract(Contract contract)
        {
            throw new NotImplementedException();
        }
        
        public void DeleteContractByHash(UInt160 contractHash)
        {
            throw new NotImplementedException();
        }
    }
}