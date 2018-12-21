using Phorkus.Core.Blockchain.State;
using Phorkus.Proto;
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
            var balances = snapshot.Balances;
            var error = Verify(transaction);
            if (error != OperatingError.Ok)
                return error;
            var confirm = transaction.Confirm;
            var assetName = confirm.BlockchainType == BlockchainType.Bitcoin
                ? snapshot.Assets.GetAssetByName("BTC").Hash
                : snapshot.Assets.GetAssetByName("ETH").Hash;
            var supply = snapshot.Assets.GetAssetSupplyByHash(assetName);
            if (!confirm.Value.IsZero() && supply >= new Money(confirm.Value))
            {
                balances.SubWithdrawingBalance(transaction.From, assetName, new Money(confirm.Value));
                snapshot.Assets.SubSupply(assetName, new Money(confirm.Value));
                balances.AddBalance(confirm.Recipient, assetName, new Money(confirm.Value));
            }

            /* TODO: "invoke smart-contract code here" */
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
            if (confirm.Recipient is null)
                return OperatingError.InvalidTransaction;
            if (confirm.Value is null)
                return OperatingError.InvalidTransaction;
            if (!_validatorManager.CheckValidator(transaction.From))
            {
                return OperatingError.InvalidTransaction;
            }

            throw new OperationNotSupportedException();
        }
    }
}