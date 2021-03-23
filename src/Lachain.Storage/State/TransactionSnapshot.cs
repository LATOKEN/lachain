using System;
using System.Linq;
using System.Runtime.CompilerServices;
using Google.Protobuf;
using Lachain.Proto;
using Lachain.Utility.Serialization;
using Lachain.Utility.Utils;

namespace Lachain.Storage.State
{
    public class TransactionSnapshot : ITransactionSnapshot
    {
        private readonly IStorageState _state;

        // public ulong Version => _state.CurrentVersion;
        public ulong Version
        {
            get
            {
                return _state.CurrentVersion;
            }
            set
            {
                _state.CurrentVersion = value;
            }
        }

        public TransactionSnapshot(IStorageState state)
        {
            _state = state;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Commit()
        {
            // Console.WriteLine($"Transaction Commit: {_state.Hash}");
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
    }
}