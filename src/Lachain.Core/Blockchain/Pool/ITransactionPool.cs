using System;
using System.Collections.Generic;
using Lachain.Core.Blockchain.Error;
using Lachain.Proto;

namespace Lachain.Core.Blockchain.Pool
{
    public interface ITransactionPool
    {
        event EventHandler<TransactionReceipt>? TransactionAdded;

        IReadOnlyDictionary<UInt256, TransactionReceipt> Transactions { get; }

        TransactionReceipt? GetByHash(UInt256 hash);

        void Restore();

        IEnumerable<UInt256> GetTransactionPoolRepository();

        OperatingError Add(Transaction transaction, Signature signature, bool notify = true);

        OperatingError Add(TransactionReceipt receipt, bool notify = true);

        IReadOnlyCollection<TransactionReceipt> Peek(int txsToLook, int txsToTake);

        void Relay(IEnumerable<TransactionReceipt> receipts);

        uint Size();

        void Delete(UInt256 transactionHash);
        void Clear();
        void ClearRepository();
        ulong GetNextNonceForAddress(UInt160 address);

    }
}