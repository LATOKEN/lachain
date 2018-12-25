using Phorkus.Core.Blockchain.State;
using Phorkus.Proto;
using Phorkus.Utility;

namespace Phorkus.Core.Blockchain.OperationManager.TransactionManager
{
    public class WithdrawTransactionExecuter : ITransactionExecuter
    {
        private readonly IValidatorManager _validatorManager;

        public WithdrawTransactionExecuter(IValidatorManager validatorManager)
        {
            _validatorManager = validatorManager;
        }

        public OperatingError Execute(Block block, Transaction transaction, IBlockchainSnapshot snapshot)
        {
            var balances = snapshot.Balances;
            var error = Verify(transaction);
            if (error != OperatingError.Ok)
                return error;
            var assetHash = transaction.Withdraw.AssetHash;
            balances.AddWithdrawingBalance(transaction.From, assetHash, new Money(transaction.Withdraw.Value));
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
            if (confirm?.Recipient is null)
                return OperatingError.InvalidTransaction;
            if (confirm?.Value is null)
                return OperatingError.InvalidTransaction;
            if (!_validatorManager.CheckValidator(transaction.From))
                return OperatingError.InvalidTransaction;
            throw new OperationNotSupportedException();
        }
    }
}