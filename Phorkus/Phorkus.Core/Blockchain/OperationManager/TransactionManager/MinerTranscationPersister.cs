using Phorkus.Core.Proto;

namespace Phorkus.Core.Blockchain.OperationManager.TransactionManager
{
    public class MinerTranscationPersister : ITransactionPersister
    {
        public OperatingError Persist(Transaction transaction, UInt256 hash)
        {
            throw new System.NotImplementedException();
        }
        
        public OperatingError Verify(Transaction transaction)
        {
            throw new System.NotImplementedException();
        }
    }
}