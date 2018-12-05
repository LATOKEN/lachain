using System.Collections.Generic;

namespace Phorkus.CrossChain.Ethereum
{
    public class EthereumTransactionCrawler : ITransactionCrawler
    {
        public ulong CurrentBlockHeight { get; }
        
        public IEnumerable<IContractTransaction> GetTransactionsAtBlock(byte[] recipient, ulong blockHeight)
        {
            throw new System.NotImplementedException();
        }
    }
}