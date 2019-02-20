using Phorkus.Proto;
using Phorkus.Storage.State;

namespace Phorkus.Core.Blockchain.OperationManager
{
    public interface ITransactionExecuter
    {
        OperatingError Execute(Block block, TransactionReceipt receipt, IBlockchainSnapshot snapshot);
        
        OperatingError Verify(Transaction transaction);
    }
}