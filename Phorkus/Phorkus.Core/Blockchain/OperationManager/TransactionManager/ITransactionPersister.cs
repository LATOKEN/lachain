using Phorkus.Core.Proto;

namespace Phorkus.Core.Blockchain.OperationManager.TransactionManager
{
    public interface ITransactionPersister
    {
        bool Persist(Transaction transaction, UInt256 hash);

        OperatingError Verify(Transaction transaction);
    }
}