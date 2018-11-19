using Phorkus.Core.Proto;

namespace Phorkus.Core.Blockchain.OperationManager.TransactionManager
{
    public interface ITransactionPersister
    {
        OperatingError Persist(Transaction transaction, UInt256 hash);

        OperatingError Verify(Transaction transaction);
    }
}