namespace Phorkus.CrossChain
{
    public interface ITransactionService
    {

        bool StoreTransaction(ITransactionData transactionData);

        bool CommitTransactionsBatch(ITransactionData[] transactionData);
        
        bool CommitTransaction(ITransactionData transactionData);
    }
}