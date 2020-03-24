using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Google.Protobuf;
using Lachain.Proto;

namespace Lachain.Storage.State
{
    public class TransactionSnapshot : ITransactionSnapshot
    {
        private readonly IStorageState _state;

        public ulong Version => _state.CurrentVersion;

        public TransactionSnapshot(IStorageState state)
        {
            _state = state;
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
            return raw == null ? 0UL : BitConverter.ToUInt64(raw, 0);
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
                throw new Exception($"This should never happen, transaction nonce mismatch: {receipt.Transaction.Nonce} but should be {expectedNonce}");
            /* save transaction status */
            receipt.Status = status;
            /* write transaction to storage */
            _state.AddOrUpdate(EntryPrefix.TransactionByHash.BuildPrefix(receipt.Hash),
                receipt.ToByteArray());
            /* update current address nonce */
            _state.AddOrUpdate(EntryPrefix.TransactionCountByFrom.BuildPrefix(receipt.Transaction.From), BitConverter.GetBytes(expectedNonce + 1));
        }
    }
}