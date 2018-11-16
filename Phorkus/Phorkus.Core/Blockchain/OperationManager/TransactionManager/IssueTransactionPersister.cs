using Phorkus.Core.Proto;
using Phorkus.Core.Storage;

namespace Phorkus.Core.Blockchain.OperationManager.TransactionManager
{
    public class IssueTransactionPersister : ITransactionPersister
    {
        private readonly IAssetRepository _assetRepository;

        public IssueTransactionPersister(IAssetRepository assetRepository)
        {
            _assetRepository = assetRepository;
        }

        public bool Persist(Transaction transaction, UInt256 hash)
        {
            throw new System.NotImplementedException();
        }
        
        public OperatingError Verify(Transaction transaction)
        {
            throw new System.NotImplementedException();
        }
    }
}