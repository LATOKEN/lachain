using Phorkus.Proto;

namespace Phorkus.Storage.State
{
    public interface ITransactionSnapshot : ISnapshot
    {
        ulong GetTotalTransactionCount(UInt160 @from);
        
        TransactionReceipt GetTransactionByHash(UInt256 transactionHash);
        
        void AddTransaction(TransactionReceipt transactionReceipt, TransactionStatus transactionStatus);
    }
}