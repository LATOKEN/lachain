using Lachain.Proto;

namespace Lachain.Storage.State
{
    public interface ITransactionSnapshot : ISnapshot
    {
        ulong GetTotalTransactionCount(UInt160 @from);
        
        TransactionReceipt? GetTransactionByHash(UInt256 transactionHash);
        
        void AddTransaction(TransactionReceipt receipt, TransactionStatus status);

        void AddToTouch(TransactionReceipt receipt);

        void TouchAll();
    }
}