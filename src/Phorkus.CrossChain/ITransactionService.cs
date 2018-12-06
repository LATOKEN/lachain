using System.Collections.Generic;

namespace Phorkus.CrossChain
{
    public interface ITransactionService
    {
        ulong CurrentBlockHeight { get; }
        
        IEnumerable<IContractTransaction> GetTransactionsAtBlock(byte[] recipient, ulong blockHeight);
        
        byte[] BroadcastTransaction(ITransactionData transactionData);
    }
}