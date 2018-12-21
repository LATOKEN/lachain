using Phorkus.Core.Blockchain.State;
using Phorkus.Proto;
using Phorkus.Utility;
using Phorkus.Utility.Utils;

namespace Phorkus.Core.Blockchain.OperationManager.TransactionManager
{
    public class WithdrawTransactionExecuter : ITransactionExecuter
    {
        private readonly IValidatorManager _validatorManager;
        private readonly IWithdrawalManager _withdrawalManager;

        public WithdrawTransactionExecuter(IValidatorManager validatorManager, IWithdrawalManager withdrawalManager)
        {
            _validatorManager = validatorManager;
            _withdrawalManager = withdrawalManager;
        }

        public OperatingError Execute(Block block, Transaction transaction, IBlockchainSnapshot snapshot)
        {
            var balances = snapshot.Balances;
            var error = Verify(transaction);
            if (error != OperatingError.Ok)
                return error;
            var withdraw = transaction.Withdraw;
            var assetName = withdraw.BlockchainType == BlockchainType.Bitcoin
                ? snapshot.Assets.GetAssetByName("BTC").Hash
                : snapshot.Assets.GetAssetByName("ETH").Hash;
            var supply = snapshot.Assets.GetAssetSupplyByHash(assetName);
            if (!withdraw.Value.IsZero() && supply >= new Money(withdraw.Value))
            {
                _withdrawalManager.AddWithdrawal(transaction);
                balances.AddWithdrawingBalance(transaction.From, assetName, new Money(withdraw.Value));
            }

            /* TODO: "invoke smart-contract code here" */
            return OperatingError.Ok;
        }

        public OperatingError Verify(Transaction transaction)
        {
            if (transaction.Type != TransactionType.Withdraw)
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