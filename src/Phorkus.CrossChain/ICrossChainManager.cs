namespace Phorkus.CrossChain
{
    public interface ICrossChainManager
    {
        ITransactionFactory GetTransactionFactory(BlockchainType blockchainType);

        ITransactionBroadcaster GetTransactionBroadcaster(BlockchainType blockchainType);
        
        ITransactionCrawler GetTransactionCrawler(BlockchainType blockchainType);
    }
}