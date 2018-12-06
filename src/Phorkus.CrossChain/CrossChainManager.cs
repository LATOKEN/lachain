using System;
using System.Collections.Generic;
using Phorkus.CrossChain.Bitcoin;
using Phorkus.CrossChain.Ethereum;
using Phorkus.Proto;

namespace Phorkus.CrossChain
{
    public class CrossChainManager : ICrossChainManager
    {
        private readonly IDictionary<BlockchainType, ITransactionFactory> _transactionFactories;
        private readonly IDictionary<BlockchainType, ITransactionService> _transactionServices;

        public CrossChainManager()
        {
            _transactionFactories = new Dictionary<BlockchainType, ITransactionFactory>
            {
                {BlockchainType.Bitcoin, new BitcoinTransactionFactory()},
                {BlockchainType.Ethereum, new EthereumTransactionFactory()}
            };
            _transactionServices = new Dictionary<BlockchainType, ITransactionService>
            {
                {BlockchainType.Bitcoin, new BitcoinTransactionService()},
                {BlockchainType.Ethereum, new EthereumTransactionService()}
            };
        }

        public ITransactionFactory GetTransactionFactory(BlockchainType blockchainType)
        {
            if (_transactionFactories.TryGetValue(blockchainType, out var result))
                return result;
            throw new ArgumentOutOfRangeException(nameof(blockchainType));
        }

        public ITransactionService GetTransactionService(BlockchainType blockchainType)
        {
            if (_transactionServices.TryGetValue(blockchainType, out var result))
                return result;
            throw new ArgumentOutOfRangeException(nameof(blockchainType));
        }
    }
}