using System;
using System.Collections.Generic;
using NeoSharp.BinarySerialization;
using NeoSharp.Core;
using NeoSharp.Core.Models;
using NeoSharp.Core.Storage.Blockchain;

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
        
        public Transaction GetTransactionByHash(UInt256 hash)
        {
            var rawTransaction = _rocksDbContext.Get(hash.BuildDataTransactionKey());
            return rawTransaction == null ? null : _binarySerializer.Deserialize<Transaction>(rawTransaction);
        }

        public void AddTransaction(Transaction transaction)
        {
            _rocksDbContext.Save(transaction.Hash.BuildDataTransactionKey(), _binarySerializer.Serialize(transaction));
        }

        public IEnumerable<Transaction> GetTransactionsByHashes(IReadOnlyCollection<UInt256> hashes)
        {
            throw new NotImplementedException();
        }

        public bool ContainsTransactionByHash(UInt256 txHash)
        {
            throw new NotImplementedException();
        }
        
        public uint GetTotalTransactionCount(UInt160 address)
        {
            return 0;
        }
    }
}