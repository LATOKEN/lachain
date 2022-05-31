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
            return raw == null ? new List<UInt256>() : raw.ByteArrayToTransactionHashList();
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public bool RemoveTransaction(UInt256 txHash)
        {
            /* remove transaction hash from pool */
            var raw = _rocksDbContext.Get(EntryPrefix.TransactionPool.BuildPrefix());
            if (raw == null)
                return false;
            var pool = raw.ByteArrayToTransactionHashList();
            if (!pool.Remove(txHash))
                return false;
            _rocksDbContext.Save(EntryPrefix.TransactionPool.BuildPrefix(), pool.TransactionHashListToByteArray());
            /* remove transaction from storage */
            _rocksDbContext.Delete(EntryPrefix.TransactionByHash.BuildPrefix(txHash));
            return true;
        }

        // use this method for bulk removal of transactions
        // as this is much faster than deleting transaction one by one
        [MethodImpl(MethodImplOptions.Synchronized)]
        public int RemoveTransactions(IEnumerable<UInt256> txHashes)
        {
            HashSet<UInt256> txHashSet = new HashSet<UInt256>();
            foreach(var txHash in txHashes)
                txHashSet.Add(txHash);
            
            var pool = GetTransactionPool();
            List<UInt256> newPool = new List<UInt256>();
            List<UInt256> txRemoved = new List<UInt256>();

            foreach(var txHash in pool)
            {
                if (!txHashSet.Contains(txHash))
                    newPool.Add(txHash);
                else 
                    txRemoved.Add(txHash);
            }
            
            _rocksDbContext.Save(EntryPrefix.TransactionPool.BuildPrefix(), newPool.TransactionHashListToByteArray());

            foreach(var txHash in txRemoved)
                _rocksDbContext.Delete(EntryPrefix.TransactionByHash.BuildPrefix(txHash));
                       
            return txRemoved.Count;
        }
        

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void AddTransaction(TransactionReceipt transaction)
        {
            /* write transaction to storage */
            var prefixTx = EntryPrefix.TransactionByHash.BuildPrefix(transaction.Hash);
            _rocksDbContext.Save(prefixTx, transaction.ToByteArray());
            /* add transaction to pool */
            var raw = _rocksDbContext.Get(EntryPrefix.TransactionPool.BuildPrefix());
            var pool = raw != null ? raw.ByteArrayToTransactionHashList() : new List<UInt256>();
            if (pool.Contains(transaction.Hash))
                return;
            pool.Add(transaction.Hash);
            _rocksDbContext.Save(EntryPrefix.TransactionPool.BuildPrefix(), pool.TransactionHashListToByteArray());
        }
        
        [MethodImpl(MethodImplOptions.Synchronized)]
        public bool ContainsTransactionByHash(UInt256 txHash)
        {
            var prefix = EntryPrefix.TransactionByHash.BuildPrefix(txHash);
            var raw = _rocksDbContext.Get(prefix);
            return raw != null;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void AddAndRemoveTx(TransactionReceipt txToAdd, TransactionReceipt txToRemove)
        {
            /* write transaction to storage */
            var batch = new RocksDbAtomicWrite(_rocksDbContext);
            var prefixTx = EntryPrefix.TransactionByHash.BuildPrefix(txToRemove.Hash);
            batch.Delete(prefixTx);
            prefixTx = EntryPrefix.TransactionByHash.BuildPrefix(txToAdd.Hash);
            batch.Put(prefixTx, txToAdd.ToByteArray());
            /* add transaction to pool */
            var pool = GetTransactionPool();
            pool.Remove(txToRemove.Hash);
            if (!pool.Contains(txToAdd.Hash))
                pool.Add(txToAdd.Hash);
            prefixTx = EntryPrefix.TransactionPool.BuildPrefix();
            batch.Put(prefixTx, pool.TransactionHashListToByteArray());
            batch.Commit();
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void ClearPool()
        {
            var pool = GetTransactionPool();
            foreach(var txHash in pool)
            {
                if(txHash is null) continue;
                _rocksDbContext.Delete(EntryPrefix.TransactionByHash.BuildPrefix(txHash));
            }
            _rocksDbContext.Delete(EntryPrefix.TransactionPool.BuildPrefix());
        }
    }
}