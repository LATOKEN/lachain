using System;
using Phorkus.CrossChain.Bitcoin;
using Phorkus.CrossChain.Ethereum;

namespace Phorkus.CrossChain
{
    public class CrossChainManager : ICrossChainManager
    {
        public ITransactionFactory GetTransactionFactory(BlockchainType blockchainType)
        {
            switch (blockchainType)
            {
                case BlockchainType.Bitcoin:
                    return new BitcoinTransactionFactory();
                case BlockchainType.Ethereum:
                    return new EthereumTransactionFactory();
            }
            throw new ArgumentOutOfRangeException(nameof(blockchainType), blockchainType, null);
        }

        public ITransactionCrawler GetTransactionCrawler(BlockchainType blockchainType)
        {
            throw new NotImplementedException();
        }
    }
}