namespace Phorkus.CrossChain
{
    public interface ICrossChainManager
    {
        ITransactionFactory GetTransactionFactory(BlockchainType blockchainType);
    }
}