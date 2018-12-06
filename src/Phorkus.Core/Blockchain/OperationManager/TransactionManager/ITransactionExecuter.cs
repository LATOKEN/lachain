using Phorkus.Core.Blockchain.State;
using Phorkus.Proto;

namespace Phorkus.Core.Blockchain.OperationManager.TransactionManager
{
    public interface ITransactionExecuter
    {
        OperatingError Execute(Block block, Transaction transaction, IBlockchainSnapshot snapshot);
        
        OperatingError Verify(Transaction transaction);
    }
}