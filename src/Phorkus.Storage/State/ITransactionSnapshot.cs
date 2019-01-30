using Phorkus.Proto;

namespace Phorkus.Storage.State
{
    public interface ITransactionSnapshot : ISnapshot
    {
        ulong GetTotalTransactionCount(UInt160 @from);
        
        AcceptedTransaction GetTransactionByHash(UInt256 transactionHash);
        
        void AddTransaction(AcceptedTransaction acceptedTransaction, TransactionStatus transactionStatus);
    }
}