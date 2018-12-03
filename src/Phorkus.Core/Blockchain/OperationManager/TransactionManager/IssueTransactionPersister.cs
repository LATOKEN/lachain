using Phorkus.Proto;
using Phorkus.Core.Storage;
using Phorkus.Core.Utils;

namespace Phorkus.Core.Blockchain.OperationManager.TransactionManager
{
    public class IssueTransactionPersister : ITransactionPersister
    {
        private readonly IAssetRepository _assetRepository;
        private readonly IBalanceRepository _balanceRepository;
        
        public IssueTransactionPersister(
            IAssetRepository assetRepository,
            IBalanceRepository balanceRepository)
        {
            _assetRepository = assetRepository;
            _balanceRepository = balanceRepository;
        }
        
        public OperatingError Execute(Block block, Transaction transaction)
        {
            var result = Verify(transaction);
            if (result != OperatingError.Ok)
                return result;
            var issue = transaction.Issue;
            /* special check for asset existence */
            var asset = _assetRepository.GetAssetByHash(issue.Asset);
            if (asset is null)
                return OperatingError.AssetNotFound;
            if (asset.Minter is null || asset.Minter.IsZero() || !asset.Minter.Equals(transaction.From))
                return OperatingError.AssetCannotBeIssued;
            if (!asset.Owner.Equals(transaction.From))
                return OperatingError.InvalidOwner;
            /* check amount recipient */
            var to = issue.To;
            if (to is null || to.IsZero())
                to = transaction.From;
            _balanceRepository.AddBalance(to, issue.Asset, new Money(issue.Supply));
            return OperatingError.Ok;
        }
        
        public OperatingError Verify(Transaction transaction)
        {
            if (transaction.Type != TransactionType.Issue)
                return OperatingError.InvalidTransaction;
            var issue = transaction.Issue;
            if (issue?.Asset is null || issue.Asset.IsZero())
                return OperatingError.InvalidTransaction;
            if (issue.Supply is null || issue.Supply.IsZero())
                return OperatingError.InvalidTransaction;
            return OperatingError.Ok;
        }
    }
}