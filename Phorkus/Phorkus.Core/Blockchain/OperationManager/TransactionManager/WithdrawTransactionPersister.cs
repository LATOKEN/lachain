using Phorkus.Core.Proto;

namespace Phorkus.Core.Blockchain.OperationManager.TransactionManager
{
    public class WithdrawTransactionPersister : ITransactionPersister
    {
        public OperatingError Confirm(Transaction transaction)
        {
            throw new OperationNotSupportedException();
        }
        
        public OperatingError Verify(Transaction transaction)
        {
            throw new OperationNotSupportedException();
        }
    }
}