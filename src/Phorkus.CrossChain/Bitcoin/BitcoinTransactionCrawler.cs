using System.Collections.Generic;

namespace Phorkus.CrossChain.Bitcoin
{
    public class BitcoinTransactionCrawler : ITransactionCrawler
    {
        public ulong CurrentBlockHeight { get; }
        
        public IEnumerable<IContractTransaction> GetTransactionsAtBlock(byte[] recipient, ulong blockHeight)
        {
            throw new System.NotImplementedException();
        }
    }
}