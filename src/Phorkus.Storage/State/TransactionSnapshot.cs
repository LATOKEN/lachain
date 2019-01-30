using System;
using System.Runtime.CompilerServices;
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

        public ulong GetTotalTransactionCount(UInt160 from)
        {
            var raw = _state.Get(EntryPrefix.TransactionCountByFrom.BuildPrefix(from));
            return raw == null ? 0UL : BitConverter.ToUInt64(raw, 0);
        }
        
        public AcceptedTransaction GetTransactionByHash(UInt256 transactionHash)
        {
            var raw = _state.Get(EntryPrefix.TransactionByHash.BuildPrefix(transactionHash));
            return raw != null ? AcceptedTransaction.Parser.ParseFrom(raw) : null;
        }

        public void AddTransaction(AcceptedTransaction acceptedTransaction, TransactionStatus transactionStatus)
        {
            /* check transaction nonce just in case */
            var nonce = GetTotalTransactionCount(acceptedTransaction.Transaction.From);
            if (nonce != acceptedTransaction.Transaction.Nonce)
                throw new Exception($"Wait, you can't add to storage transaction with this nonce {acceptedTransaction.Transaction.Nonce}, cuz excepted {nonce}, check it before");
            /* save transaction status */
            acceptedTransaction.Status = transactionStatus;
            /* write transaction to storage */
            _state.AddOrUpdate(EntryPrefix.TransactionByHash.BuildPrefix(acceptedTransaction.Hash),
                acceptedTransaction.ToByteArray());
            /* update current address nonce */
            _state.AddOrUpdate(EntryPrefix.TransactionCountByFrom.BuildPrefix(acceptedTransaction.Transaction.From), BitConverter.GetBytes(nonce + 1));
        }
    }
}