using Phorkus.Proto;

namespace Phorkus.Core.Blockchain.OperationManager.TransactionManager
{
    public interface ITransactionPersister
    {
        OperatingError Execute(Block block, Transaction transaction);
        
        OperatingError Verify(Transaction transaction);
    }
}