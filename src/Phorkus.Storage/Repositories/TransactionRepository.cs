using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Google.Protobuf;
using Phorkus.Proto;
using Phorkus.Utility.Utils;

namespace Phorkus.Storage.Repositories
{
    public class TransactionRepository : ITransactionRepository
    {
        private readonly IRocksDbContext _rocksDbContext;
        
        public TransactionRepository(IRocksDbContext rocksDbContext)
        {
            _rocksDbContext = rocksDbContext ?? throw new ArgumentNullException(nameof(rocksDbContext));
        }
        
        [MethodImpl(MethodImplOptions.Synchronized)]
        public SignedTransaction GetTransactionByHash(UInt256 txHash)
        {
            var prefix = EntryPrefix.TransactionByHash.BuildPrefix(txHash);
            var raw = _rocksDbContext.Get(prefix);
            return raw == null ? null : SignedTransaction.Parser.ParseFrom(raw);
        }
        
        [MethodImpl(MethodImplOptions.Synchronized)]
        public ICollection<UInt256> GetTransactionPool()
        {
            var raw = _rocksDbContext.Get(EntryPrefix.TransactionPool.BuildPrefix());
            return raw == null ? new List<UInt256>() : raw.ToMessageArray<UInt256>();
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public bool RemoveFromTransactionFromPool(UInt256 txHash)
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
        public void AddTransaction(SignedTransaction transaction)
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
        public TransactionState ChangeTransactionState(UInt256 txHash, TransactionState txState)
        {
            if (txHash is null)
                throw new ArgumentNullException(nameof(txHash));
            var prefix = EntryPrefix.TransactionStateByHash.BuildPrefix(txHash);
            var rawPrevState = _rocksDbContext.Get(prefix);
            _rocksDbContext.Save(prefix, txState.ToByteArray());
            return rawPrevState != null ? TransactionState.Parser.ParseFrom(rawPrevState) : null;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public TransactionState GetTransactionState(UInt256 txHash)
        {
            if (txHash is null)
                throw new ArgumentNullException(nameof(txHash));
            var prefix = EntryPrefix.TransactionStateByHash.BuildPrefix(txHash);
            var rawPrevState = _rocksDbContext.Get(prefix);
            return rawPrevState != null ? TransactionState.Parser.ParseFrom(rawPrevState) : null;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public IEnumerable<SignedTransaction> GetTransactionsByHashes(IEnumerable<UInt256> txHashes)
        {
            return txHashes.Select(GetTransactionByHash).Where(tx => tx != null);
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public bool ContainsTransactionByHash(UInt256 txHash)
        {
            var prefix = EntryPrefix.TransactionByHash.BuildPrefix(txHash);
            var raw = _rocksDbContext.Get(prefix);
            return raw != null;
        }
        
        [MethodImpl(MethodImplOptions.Synchronized)]
        public SignedTransaction GetLatestTransactionByFrom(UInt160 from)
        {
            var prefix = EntryPrefix.TransactionLatestByFrom.BuildPrefix(from);
            var rawHash = _rocksDbContext.Get(prefix);
            if (rawHash is null)
                return null;
            var hash = UInt256.Parser.ParseFrom(rawHash);
            return GetTransactionByHash(hash);
        }
        
        [MethodImpl(MethodImplOptions.Synchronized)]
        public void CommitTransaction(SignedTransaction signedTransaction, UInt256 blockHash)
        {
            /* remove transaction hash from pool */
            var raw = _rocksDbContext.Get(EntryPrefix.TransactionPool.BuildPrefix());
            var pool = raw != null ? raw.ToMessageArray<UInt256>() : new List<UInt256>();
            if (pool.Remove(signedTransaction.Hash))
                _rocksDbContext.Save(EntryPrefix.TransactionPool.BuildPrefix(), pool.ToByteArray());
            /* change transaction hash */
            raw = _rocksDbContext.Get(EntryPrefix.TransactionByHash.BuildPrefix(signedTransaction.Hash));
            if (raw is null)
                throw new ArgumentNullException(nameof(signedTransaction.Hash), "This should never happen");
            var tx = SignedTransaction.Parser.ParseFrom(raw);
            tx.Block = blockHash;
            _rocksDbContext.Save(EntryPrefix.TransactionByHash.BuildPrefix(signedTransaction.Hash), tx.ToByteArray());
            /* write latest transaction hash by from */
            var prefixHash = EntryPrefix.TransactionLatestByFrom.BuildPrefix(signedTransaction.Transaction.From);
            _rocksDbContext.Save(prefixHash, signedTransaction.Hash.ToByteArray());
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public uint GetTotalTransactionCount(UInt160 from)
        {
            var latestTx = GetLatestTransactionByFrom(from);
            return (uint) (latestTx?.Transaction.Nonce + 1 ?? 0);
        }
    }
}