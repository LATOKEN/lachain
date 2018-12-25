using Phorkus.Proto;
using Phorkus.Storage.State;
using Phorkus.Utility;
using Phorkus.Utility.Utils;

namespace Phorkus.Core.Blockchain.OperationManager.TransactionManager
{
    public class ConfirmTransactionExecuter : ITransactionExecuter
    {
        private readonly IValidatorManager _validatorManager;

        public ConfirmTransactionExecuter(IValidatorManager validatorManager)
        {
            _validatorManager = validatorManager;
        }

        public OperatingError Execute(Block block, Transaction transaction, IBlockchainSnapshot snapshot)
        {
            var balanceRepository = snapshot.Balances;
            var error = Verify(transaction);
            if (error != OperatingError.Ok)
                return error;
            var confirm = transaction.Confirm;
            var asset = snapshot.Assets.GetAssetByHash(confirm.AssetHash);
            if (asset is null)
                return OperatingError.UnknownAsset;
            var assetHash = asset.Hash;
            if (confirm.Value.IsZero() || asset.Supply.ToMoney() < new Money(confirm.Value))
                return OperatingError.Ok;
            balanceRepository.SubWithdrawingBalance(transaction.From, assetHash, new Money(confirm.Value));
            balanceRepository.AddAvailableBalance(confirm.Recipient, assetHash, new Money(confirm.Value));
            return OperatingError.Ok;
        }

        public OperatingError Verify(Transaction transaction)
        {
            if (transaction.Type != TransactionType.Confirm)
                return OperatingError.InvalidTransaction;
            var confirm = transaction.Deposit;
            if (confirm?.BlockchainType is null)
                return OperatingError.InvalidTransaction;
            if (confirm?.TransactionHash is null)
                return OperatingError.InvalidTransaction;
            if (confirm?.Timestamp is null)
                return OperatingError.InvalidTransaction;
            if (confirm?.AddressFormat is null)
                return OperatingError.InvalidTransaction;
            if (confirm?.Recipient is null)
                return OperatingError.InvalidTransaction;
            if (confirm?.Value is null)
                return OperatingError.InvalidTransaction;
            if (confirm?.AssetHash is null)
                return OperatingError.InvalidTransaction;
            if (!_validatorManager.CheckValidator(transaction.From))
                return OperatingError.InvalidTransaction;
            throw new OperationNotSupportedException();
        }
    }
}