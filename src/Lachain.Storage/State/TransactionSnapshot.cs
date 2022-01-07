using System;
using System.Linq;
using System.Runtime.CompilerServices;
using Google.Protobuf;
using Lachain.Proto;
using Lachain.Utility.Serialization;
using Lachain.Storage.Trie;
using System.Collections.Generic;
using Lachain.Logger;

namespace Lachain.Storage.State
{
    public class TransactionSnapshot : ITransactionSnapshot
    {

        private static readonly ILogger<TransactionSnapshot> Logger = LoggerFactory.GetLoggerForClass<TransactionSnapshot>();
        private readonly IStorageState _state;

        public ulong Version => _state.CurrentVersion;

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
        public void Commit()
        {
            _state.Commit();
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
            Logger.LogTrace($"Before reading expectedNonce");
            var expectedNonce = GetTotalTransactionCount(receipt.Transaction.From);
            Logger.LogTrace($"After reading expectedNonce");
            if (expectedNonce != receipt.Transaction.Nonce)
                throw new Exception(
                    $"This should never happen, transaction nonce mismatch: {receipt.Transaction.Nonce} but should be {expectedNonce}");
            /* save transaction status */
            receipt.Status = status;
            /* write transaction to storage */

            Logger.LogTrace($"Adding tx hash to the database");
            _state.AddOrUpdate(EntryPrefix.TransactionByHash.BuildPrefix(receipt.Hash),
                receipt.ToByteArray());
            /* update current address nonce */

            Logger.LogTrace($"Adding nonce to the database");
            _state.AddOrUpdate(
                EntryPrefix.TransactionCountByFrom.BuildPrefix(receipt.Transaction.From),
                (expectedNonce + 1).ToBytes().ToArray()
            );
            Logger.LogTrace($"Done adding transaction");
        }

        public void AddToTouch(TransactionReceipt receipt)
        {
            _state.AddToTouch(EntryPrefix.TransactionByHash.BuildPrefix(receipt.Hash));
            _state.AddToTouch(EntryPrefix.TransactionCountByFrom.BuildPrefix(receipt.Transaction.From));
        }

        public void TouchAll()
        {
            _state.TouchAll();
        }

        public void SetCurrentVersion(ulong root)
        {
            _state.SetCurrentVersion(root);
        }
    }
}