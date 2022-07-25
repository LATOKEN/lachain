using System;
using System.Linq;
using System.Runtime.CompilerServices;
using Google.Protobuf;
using Lachain.Proto;
using Lachain.Utility.Serialization;
using Lachain.Storage.Trie;
using System.Collections.Generic;
using Lachain.Storage.DbCompact;

namespace Lachain.Storage.State
{
    /*
        Transaction Snapshot is HMAT (trie) which acts as an key-value storage to store the following infos: 
        (1) (transaction hash -> transaction receipt)
        (2) (address -> count of transactions from this address)
    */
    public class TransactionSnapshot : ITransactionSnapshot
    {
        private readonly IStorageState _state;

        public ulong Version => _state.CurrentVersion;
        public uint RepositoryId => _state.RepositoryId;

        public TransactionSnapshot(IStorageState state)
        {
            _state = state;
        }
        public IDictionary<ulong,IHashTrieNode> GetState()
        {
            return _state.GetAllNodes();
        }

        public bool IsTrieNodeHashesOk()
        {
            return _state.IsNodeHashesOk();
        }
        
        public ulong SetState(ulong root, IDictionary<ulong, IHashTrieNode> allTrieNodes)
        {
            return _state.InsertAllNodes(root, allTrieNodes);
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Commit(RocksDbAtomicWrite batch)
        {
            _state.Commit(batch);
        }

        public UInt256 Hash => _state.Hash;

        public ulong GetTotalTransactionCount(UInt160 from)
        {
            var raw = _state.Get(EntryPrefix.TransactionCountByFrom.BuildPrefix(from));
            return raw?.AsReadOnlySpan().ToUInt64() ?? 0u;
        }

        public TransactionReceipt? GetTransactionByHash(UInt256 transactionHash)
        {
            var raw = _state.Get(EntryPrefix.TransactionByHash.BuildPrefix(transactionHash));
            return raw != null ? TransactionReceipt.Parser.ParseFrom(raw) : null;
        }

        public void AddTransaction(TransactionReceipt receipt, TransactionStatus status)
        {
            var expectedNonce = GetTotalTransactionCount(receipt.Transaction.From);
            if (expectedNonce != receipt.Transaction.Nonce)
                throw new Exception(
                    $"This should never happen, transaction nonce mismatch: {receipt.Transaction.Nonce} but should be {expectedNonce}");
            /* save transaction status */
            receipt.Status = status;
            /* write transaction to storage */
            _state.AddOrUpdate(EntryPrefix.TransactionByHash.BuildPrefix(receipt.Hash),
                receipt.ToByteArray());
            /* update current address nonce */
            _state.AddOrUpdate(
                EntryPrefix.TransactionCountByFrom.BuildPrefix(receipt.Transaction.From),
                (expectedNonce + 1).ToBytes().ToArray()
            );
        }

        public void SetCurrentVersion(ulong root)
        {
            _state.SetCurrentVersion(root);
        }
        public void ClearCache()
        {
            _state.ClearCache();
        }

        public ulong SaveNodeId(IDbShrinkRepository _repo)
        {
            return _state.SaveNodeId(_repo);
        }

        public ulong DeleteNodeId(IDbShrinkRepository _repo)
        {
            return _state.DeleteNodeId(_repo);
        }

        public ulong DeleteSnapshot(IDbShrinkRepository _repo)
        {
            return _state.DeleteNodes(_repo);
        }

    }
}