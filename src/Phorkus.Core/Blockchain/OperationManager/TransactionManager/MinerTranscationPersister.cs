using Phorkus.Proto;

namespace Phorkus.Core.Blockchain.OperationManager.TransactionManager
{
    public class MinerTranscationPersister : ITransactionPersister
    {
        public OperatingError Execute(Block block, Transaction transaction)
        {
            return OperatingError.Ok;
        }
        
        public OperatingError Verify(Transaction transaction)
        {
            if (transaction.Type != TransactionType.Miner)
                return OperatingError.InvalidTransaction;
            return OperatingError.Ok;
        }
    }
}