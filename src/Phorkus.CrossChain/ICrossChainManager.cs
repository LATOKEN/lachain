using Phorkus.Proto;

namespace Phorkus.CrossChain
{
    public interface ICrossChainManager
    {
        ITransactionFactory GetTransactionFactory(BlockchainType blockchainType);

        ITransactionService GetTransactionService(BlockchainType blockchainType);
    }
}