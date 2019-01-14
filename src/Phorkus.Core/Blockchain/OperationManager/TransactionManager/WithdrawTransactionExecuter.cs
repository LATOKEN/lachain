using System;
using Phorkus.Core.Utils;
using Phorkus.Proto;
using Phorkus.Storage.Repositories;
using Phorkus.Storage.State;
using Phorkus.Utility;

namespace Phorkus.Core.Blockchain.OperationManager.TransactionManager
{
    public class WithdrawTransactionExecuter : ITransactionExecuter
    {
        private readonly IWithdrawalRepository _withdrawalRepository;        
        private readonly IValidatorManager _validatorManager;
        
        public WithdrawTransactionExecuter(
            IValidatorManager validatorManager,
            IWithdrawalRepository withdrawalRepository)
        {
            _validatorManager = validatorManager;
            _withdrawalRepository = withdrawalRepository;
        }

        public OperatingError Execute(Block block, Transaction transaction, IBlockchainSnapshot snapshot)
        {
            /* verify transaction before execution */
            var balances = snapshot.Balances;
            var error = Verify(transaction);
            if (error != OperatingError.Ok)
                return error;
            var assetHash = transaction.Withdraw.AssetHash;
            /* block user balances for withdrawal */
            balances.SubAvailableBalance(transaction.From, assetHash, new Money(transaction.Withdraw.Value));
            balances.AddWithdrawingBalance(transaction.From, assetHash, new Money(transaction.Withdraw.Value));
            /* register new withdrawal in queue */
            var withdrawal = new Withdrawal
            {
                TransactionHash = transaction.ToHash256(),
                OriginalHash = transaction.Withdraw.TransactionHash,
                State = WithdrawalState.Registered,
                Timestamp = (ulong) new DateTimeOffset().ToUnixTimeMilliseconds()
            };
            return !_withdrawalRepository.AddWithdrawal(withdrawal) ? OperatingError.WithdrawalFailed : OperatingError.Ok;
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