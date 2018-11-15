using System;
using System.Collections.Generic;
using Phorkus.Core.Proto;
using Phorkus.Core.Storage;

namespace Phorkus.RocksDB.Repositories
{
    public class TransactionRepository : ITransactionRepository
    {
        private readonly IRocksDbContext _rocksDbContext;
        
        public TransactionRepository(IRocksDbContext rocksDbContext)
        {
            _rocksDbContext = rocksDbContext ?? throw new ArgumentNullException(nameof(rocksDbContext));
        }
        
        public Transaction GetTransactionByHash(UInt256 hash)
        {
            throw new NotImplementedException();
            /*var rawTransaction = _rocksDbContext.Get(hash.BuildDataTransactionKey());
            return rawTransaction == null ? null : _binarySerializer.Deserialize<Transaction>(rawTransaction);*/
        }

        public void AddTransaction(Transaction transaction)
        {
            throw new NotImplementedException();
            /*_rocksDbContext.Save(transaction.Hash.BuildDataTransactionKey(), _binarySerializer.Serialize(transaction));*/
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
            throw new NotImplementedException();
        }
    }
}