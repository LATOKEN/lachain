using Phorkus.Proto;
using Phorkus.Core.Storage;
using Phorkus.Core.Storage.Repositories;

namespace Phorkus.Core.Blockchain.OperationManager.TransactionManager
{
    public class PublishTransactionPersister : ITransactionPersister
    {
        private readonly IContractRepository _contractRepository;

        public PublishTransactionPersister(IContractRepository contractRepository)
        {
            _contractRepository = contractRepository;
        }

        public OperatingError Execute(Transaction transaction)
        {
            throw new System.NotImplementedException();
        }
        
        public OperatingError Verify(Transaction transaction)
        {
            throw new System.NotImplementedException();
        }
    }
}