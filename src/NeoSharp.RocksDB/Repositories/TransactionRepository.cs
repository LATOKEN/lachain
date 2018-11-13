using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NeoSharp.BinarySerialization;
using NeoSharp.Core.Models;
using NeoSharp.Core.Storage.Blockchain;
using NeoSharp.Types;

namespace NeoSharp.RocksDB.Repositories
{
    public class TransactionRepository : ITransactionRepository
    {
        private readonly IBinarySerializer _binarySerializer;
        private readonly IRocksDbContext _rocksDbContext;
        
        public TransactionRepository(
            IRocksDbContext rocksDbContext,
            IBinarySerializer binarySerializer)
        {
            _binarySerializer = binarySerializer ?? throw new ArgumentNullException(nameof(binarySerializer));
            _rocksDbContext = rocksDbContext ?? throw new ArgumentNullException(nameof(rocksDbContext));
        }
        
        public async Task<Transaction> GetTransactionByHash(UInt256 hash)
        {
            var rawTransaction = await _rocksDbContext.Get(hash.BuildDataTransactionKey());
            return rawTransaction == null ? null : _binarySerializer.Deserialize<Transaction>(rawTransaction);
        }

        public async Task AddTransaction(Transaction transaction)
        {
            await _rocksDbContext.Save(transaction.Hash.BuildDataTransactionKey(),
                _binarySerializer.Serialize(transaction));
        }

        public async Task<IEnumerable<Transaction>> GetTransactionsByHashes(IReadOnlyCollection<UInt256> hashes)
        {
            throw new NotImplementedException();
        }

        public async Task<bool> ContainsTransactionByHash(UInt256 txHash)
        {
            throw new NotImplementedException();
        }

        public async Task<uint> GetTotalTransactionCount(UInt160 address)
        {
            throw new NotImplementedException();
        }
    }
}