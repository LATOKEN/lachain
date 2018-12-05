using System.Collections.Generic;
using System.Numerics;

namespace Phorkus.CrossChain
{
    public interface ITransactionService
    {
        ulong CurrentBlockHeight { get; }

        BigInteger GetLastBlockHeight();
        
        IEnumerable<IContractTransaction> GetTransactionsAtBlock(byte[] recipient, ulong blockHeight);
        
        bool StoreTransaction(ITransactionData transactionData);

        bool BroadcastTransactionsBatch(ITransactionData[] transactionData);
        
        bool BroadcastTransaction(ITransactionData transactionData);
    }
}