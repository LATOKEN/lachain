using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Google.Protobuf;
using Phorkus.Proto;

namespace Phorkus.Storage.State
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
        
        public TransactionReceipt GetTransactionByHash(UInt256 transactionHash)
        {
            var raw = _state.Get(EntryPrefix.TransactionByHash.BuildPrefix(transactionHash));
            return raw != null ? TransactionReceipt.Parser.ParseFrom(raw) : null;
        }
        
        public void AddTransaction(TransactionReceipt acceptedTransaction, TransactionStatus transactionStatus)
        {
            var expectedNonce = GetTotalTransactionCount(acceptedTransaction.Transaction.From);
            if (expectedNonce != acceptedTransaction.Transaction.Nonce)
                throw new Exception("This should never happen, transaction nonce mismatch");
            /* save transaction status */
            acceptedTransaction.Status = transactionStatus;
            /* write transaction to storage */
            _state.AddOrUpdate(EntryPrefix.TransactionByHash.BuildPrefix(acceptedTransaction.Hash),
                acceptedTransaction.ToByteArray());
            /* update current address nonce */
            _state.AddOrUpdate(EntryPrefix.TransactionCountByFrom.BuildPrefix(acceptedTransaction.Transaction.From), BitConverter.GetBytes(expectedNonce + 1));
        }
    }
}