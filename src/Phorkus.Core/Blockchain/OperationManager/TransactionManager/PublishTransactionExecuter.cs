using Phorkus.Core.Blockchain.State;
using Phorkus.Proto;
using Phorkus.Core.Storage;

namespace Phorkus.Core.Blockchain.OperationManager.TransactionManager
{
    public class PublishTransactionExecuter : ITransactionExecuter
    {
        private readonly IContractRepository _contractRepository;

        public PublishTransactionExecuter(IContractRepository contractRepository)
        {
            _contractRepository = contractRepository;
        }

        public OperatingError Execute(Block block, Transaction transaction, IBlockchainSnapshot snapshot)
        {
            throw new System.NotImplementedException();
        }
        
        public OperatingError Verify(Transaction transaction)
        {
            throw new System.NotImplementedException();
        }
    }
}