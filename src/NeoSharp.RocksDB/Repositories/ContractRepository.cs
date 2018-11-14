using System;
using NeoSharp.BinarySerialization;
using NeoSharp.Core;
using NeoSharp.Core.Models;
using NeoSharp.Core.Storage.Blockchain;

namespace NeoSharp.RocksDB.Repositories
{
    public class ContractRepository : IContractRepository
    {
        private readonly IBinarySerializer _binarySerializer;
        private readonly IRocksDbContext _rocksDbContext;

        public ContractRepository(
            IRocksDbContext rocksDbContext,
            IBinarySerializer binarySerializer)
        {
            _binarySerializer = binarySerializer ?? throw new ArgumentNullException(nameof(binarySerializer));
            _rocksDbContext = rocksDbContext ?? throw new ArgumentNullException(nameof(rocksDbContext));
        }

        public Contract GetContractByHash(UInt160 contractHash)
        {
            var raw = _rocksDbContext.Get(contractHash.BuildStateContractKey());
            return raw == null ? null : _binarySerializer.Deserialize<Contract>(raw);
        }
        
        public void AddContract(Contract contract)
        {
            _rocksDbContext.Save(contract.ScriptHash.BuildStateContractKey(), _binarySerializer.Serialize(contract));
        }
        
        public void DeleteContractByHash(UInt160 contractHash)
        {
            _rocksDbContext.Delete(contractHash.BuildStateContractKey());
        }
    }
}