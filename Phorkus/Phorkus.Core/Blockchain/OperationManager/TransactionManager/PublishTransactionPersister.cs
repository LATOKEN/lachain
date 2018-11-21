using Phorkus.Core.Proto;
using Phorkus.Core.Storage;

namespace Phorkus.Core.Blockchain.OperationManager.TransactionManager
{
    public class PublishTransactionPersister : ITransactionPersister
    {
        private readonly IContractRepository _contractRepository;

        public PublishTransactionPersister(IContractRepository contractRepository)
        {
            _contractRepository = contractRepository;
        }

        public OperatingError Confirm(Transaction transaction, UInt256 hash)
        {
            throw new System.NotImplementedException();
        }
        
        public OperatingError Verify(Transaction transaction)
        {
            throw new System.NotImplementedException();
        }
    }
}