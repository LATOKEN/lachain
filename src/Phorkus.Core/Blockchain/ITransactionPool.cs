using System.Collections.Generic;
using Phorkus.Core.Blockchain.OperationManager;
using Phorkus.Proto;

namespace Phorkus.Core.Blockchain
{
    public interface ITransactionPool
    {
        IReadOnlyDictionary<UInt256, TransactionReceipt> Transactions { get; }

        TransactionReceipt? GetByHash(UInt256 hash);

        void Restore();

        OperatingError Add(Transaction transaction, Signature signature);
        
        OperatingError Add(TransactionReceipt transaction);
        
        IReadOnlyCollection<TransactionReceipt> Peek(int limit = -1);

        void Relay(IEnumerable<TransactionReceipt> receipts);
        
        uint Size();
        
        void Delete(UInt256 transactionHash);

        void Clear();
    }
}