using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Google.Protobuf;
using Lachain.Proto;
using Lachain.Utility.Utils;

namespace Lachain.Storage.Repositories
{
    public class PoolRepository : IPoolRepository
    {
        private readonly IRocksDbContext _rocksDbContext;
        
        public PoolRepository(IRocksDbContext rocksDbContext)
        {
            _rocksDbContext = rocksDbContext ?? throw new ArgumentNullException(nameof(rocksDbContext));
        }
        
        [MethodImpl(MethodImplOptions.Synchronized)]
        public TransactionReceipt? GetTransactionByHash(UInt256 txHash)
        {
            var prefix = EntryPrefix.TransactionByHash.BuildPrefix(txHash);
            var raw = _rocksDbContext.Get(prefix);
            return raw == null ? null : TransactionReceipt.Parser.ParseFrom(raw);
        }
        
        [MethodImpl(MethodImplOptions.Synchronized)]
        public ICollection<UInt256> GetTransactionPool()
        {
            var raw = _rocksDbContext.Get(EntryPrefix.TransactionPool.BuildPrefix());
            return raw == null ? new List<UInt256>() : raw.ToMessageArray<UInt256>();
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public bool RemoveTransaction(UInt256 txHash)
        {
            /* remove transaction hash from pool */
            var raw = _rocksDbContext.Get(EntryPrefix.TransactionPool.BuildPrefix());
            if (raw == null)
                return false;
            var pool = raw.ToMessageArray<UInt256>();
            if (!pool.Remove(txHash))
                return false;
            _rocksDbContext.Save(EntryPrefix.TransactionPool.BuildPrefix(), pool.ToByteArray());
            /* remove transaction from storage */
            _rocksDbContext.Delete(EntryPrefix.TransactionByHash.BuildPrefix(txHash));
            return true;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public int RemoveTransactions(IEnumerable<UInt256> txHashes)
        {
            var raw = _rocksDbContext.Get(EntryPrefix.TransactionPool.BuildPrefix());
            if (raw == null)
                return 0;
                
            var pool = raw.ToMessageArray<UInt256>();
            List<UInt256> txToRemove = new List<UInt256>();

            foreach(var txHash in txHashes)
            {
                if (pool.Remove(txHash))
                {
                    txToRemove.Add(txHash);
                }
            }
            _rocksDbContext.Save(EntryPrefix.TransactionPool.BuildPrefix(), pool.ToByteArray());

            foreach(var txHash in txToRemove)
            {
                _rocksDbContext.Delete(EntryPrefix.TransactionByHash.BuildPrefix(txHash));
            }            
            return txToRemove.Count;
        }
        

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void AddTransaction(TransactionReceipt transaction)
        {
            /* write transaction to storage */
            var prefixTx = EntryPrefix.TransactionByHash.BuildPrefix(transaction.Hash);
            _rocksDbContext.Save(prefixTx, transaction.ToByteArray());
            /* add transaction to pool */
            var raw = _rocksDbContext.Get(EntryPrefix.TransactionPool.BuildPrefix());
            var pool = raw != null ? raw.ToMessageArray<UInt256>() : new List<UInt256>();
            if (pool.Contains(transaction.Hash))
                return;
            pool.Add(transaction.Hash);
            _rocksDbContext.Save(EntryPrefix.TransactionPool.BuildPrefix(), pool.ToByteArray());
        }
        
        [MethodImpl(MethodImplOptions.Synchronized)]
        public bool ContainsTransactionByHash(UInt256 txHash)
        {
            var prefix = EntryPrefix.TransactionByHash.BuildPrefix(txHash);
            var raw = _rocksDbContext.Get(prefix);
            return raw != null;
        }
    }
}