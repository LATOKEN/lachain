using System.Collections.Generic;

namespace Phorkus.CrossChain
{
    public interface ITransactionCrawler
    {
        ulong CurrentBlockHeight { get; }
        
        IEnumerable<IContractTransaction> GetTransactionsAtBlock(byte[] recipient, ulong blockHeight);
    }
}