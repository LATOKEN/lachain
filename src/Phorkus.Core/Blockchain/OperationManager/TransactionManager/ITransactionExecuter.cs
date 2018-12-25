using Phorkus.Proto;
using Phorkus.Storage.State;

namespace Phorkus.Core.Blockchain.OperationManager.TransactionManager
{
    public interface ITransactionExecuter
    {
        OperatingError Execute(Block block, Transaction transaction, IBlockchainSnapshot snapshot);
        
        OperatingError Verify(Transaction transaction);
    }
}