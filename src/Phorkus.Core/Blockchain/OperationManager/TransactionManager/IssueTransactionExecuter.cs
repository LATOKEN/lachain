using Phorkus.Core.Blockchain.State;
using Phorkus.Proto;
using Phorkus.Core.Storage;
using Phorkus.Core.Utils;
using Phorkus.Utility;
using Phorkus.Utility.Utils;

namespace Phorkus.Core.Blockchain.OperationManager.TransactionManager
{
    public class IssueTransactionExecuter : ITransactionExecuter
    {
        public IssueTransactionExecuter()
        {
        }

        public OperatingError Execute(Block block, Transaction transaction, IBlockchainSnapshot snapshot)
        {
            var balances = snapshot.Balances;
            var result = Verify(transaction);
            if (result != OperatingError.Ok)
                return result;
            var issue = transaction.Issue;
            /* special check for asset existence */
            var asset = snapshot.Assets.GetAssetByHash(issue.Asset);
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
            balances.AddBalance(to, issue.Asset, new Money(issue.Supply));
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