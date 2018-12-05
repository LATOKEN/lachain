namespace Phorkus.CrossChain.Ethereum
{
    public class EthereumTransactionService : ITransactionService
    {
        
        public bool CommitTransactionsBatch(ITransactionData[] transactionData)
        {
            return true;
        }

        public bool StoreTransaction(ITransactionData transactionData)
        {
            return true;
        }
        
        public bool CommitTransaction(ITransactionData transactionData)
        {
            return true;
        }
    }
}