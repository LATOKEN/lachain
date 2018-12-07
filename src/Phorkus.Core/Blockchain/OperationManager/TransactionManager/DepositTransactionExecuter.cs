using Phorkus.Core.Blockchain.State;
using Phorkus.Proto;

namespace Phorkus.Core.Blockchain.OperationManager.TransactionManager
{
    public class DepositTransactionExecuter : ITransactionExecuter
    {
        private readonly IValidatorManager _validatorManager;

        public DepositTransactionExecuter(IValidatorManager validatorManager)
        {
            _validatorManager = validatorManager;
        }

        public OperatingError Execute(Block block, Transaction transaction, IBlockchainSnapshot snapshot)
        {
            throw new OperationNotSupportedException();
        }
        
        public OperatingError Verify(Transaction transaction)
        {
            if (!_validatorManager.CheckValidator(transaction.From))
            {
            }
            throw new OperationNotSupportedException();
        }
    }
}