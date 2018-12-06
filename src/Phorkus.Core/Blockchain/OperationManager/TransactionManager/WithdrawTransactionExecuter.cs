using Phorkus.Core.Blockchain.State;
using Phorkus.Proto;

namespace Phorkus.Core.Blockchain.OperationManager.TransactionManager
{
    public class WithdrawTransactionExecuter : ITransactionExecuter
    {
        public OperatingError Execute(Block block, Transaction transaction, IBlockchainSnapshot snapshot)
        {
            throw new OperationNotSupportedException();
        }
        
        public OperatingError Verify(Transaction transaction)
        {
            throw new OperationNotSupportedException();
        }
    }
}