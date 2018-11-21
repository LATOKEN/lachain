using Phorkus.Core.Proto;

namespace Phorkus.Core.Blockchain.OperationManager.TransactionManager
{
    public interface ITransactionPersister
    {
        OperatingError Confirm(Transaction transaction);
        
        OperatingError Verify(Transaction transaction);
    }
}