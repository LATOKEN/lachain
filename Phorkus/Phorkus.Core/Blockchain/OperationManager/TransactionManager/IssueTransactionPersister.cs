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

        public OperatingError Confirm(Transaction transaction, UInt256 hash)
        {
            /* TODO: "implement logics here" */
            return OperatingError.Ok;
        }
        
        public OperatingError Verify(Transaction transaction)
        {
            if (transaction.Type != TransactionType.Issue)
                return OperatingError.InvalidTransaction;
            return OperatingError.Ok;
        }
    }
}