using Lachain.Proto;
using Lachain.Storage.State;

namespace Lachain.Core.Blockchain.OperationManager
{
    public interface ITransactionExecuter
    {
        OperatingError Execute(Block block, TransactionReceipt receipt, IBlockchainSnapshot snapshot);
        
        OperatingError Verify(Transaction transaction);
    }
}