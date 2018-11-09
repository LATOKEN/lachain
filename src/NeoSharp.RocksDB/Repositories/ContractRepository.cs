using System;
using System.Threading.Tasks;
using NeoSharp.BinarySerialization;
using NeoSharp.Core.Models;
using NeoSharp.Core.Storage.Blockchain;
using NeoSharp.Types;

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

        public async Task<Contract> GetContractByHash(UInt160 contractHash)
        {
            var raw = await _rocksDbContext.Get(contractHash.BuildStateContractKey());
            return raw == null ? null : _binarySerializer.Deserialize<Contract>(raw);
        }
        
        public async Task AddContract(Contract contract)
        {
            await _rocksDbContext.Save(contract.ScriptHash.BuildStateContractKey(),
                _binarySerializer.Serialize(contract));
        }
        
        public async Task DeleteContractByHash(UInt160 contractHash)
        {
            await _rocksDbContext.Delete(contractHash.BuildStateContractKey());
        }
    }
}