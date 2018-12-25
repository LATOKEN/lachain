using System.Collections.Generic;
using Phorkus.Core.Blockchain.Genesis;
using Phorkus.Core.Blockchain.OperationManager;
using Phorkus.Core.Blockchain.OperationManager.BlockManager;
using Phorkus.Core.Blockchain.OperationManager.TransactionManager;
using Phorkus.Proto;
using Phorkus.Crypto;
using Phorkus.Storage.RocksDB.Repositories;

namespace Phorkus.Core.Blockchain
{
    public class BlockchainManager : IBlockchainManager, IBlockchainContext
    {
        private readonly IGenesisBuilder _genesisBuilder;
        private readonly IBlockManager _blockManager;
        private readonly ITransactionManager _transactionManager;
        private readonly IBlockRepository _blockRepository;
        private readonly IGlobalRepository _globalRepository;

        public ulong CurrentBlockHeaderHeight => _globalRepository.GetTotalBlockHeaderHeight();
        public ulong CurrentBlockHeight => _globalRepository.GetTotalBlockHeight();

        public Block CurrentBlockHeader => _blockRepository.GetBlockByHeight(CurrentBlockHeaderHeight);
        public Block CurrentBlock => _blockRepository.GetBlockByHeight(CurrentBlockHeight);
        
        public BlockchainManager(
            IGenesisBuilder genesisBuilder,
            IBlockManager blockManager,
            ITransactionManager transactionManager,
            IBlockRepository blockRepository,
            IGlobalRepository globalRepository)
        {
            _genesisBuilder = genesisBuilder;
            _blockManager = blockManager;
            _transactionManager = transactionManager;
            _blockRepository = blockRepository;
            _globalRepository = globalRepository;
        }
        
        public bool TryBuildGenesisBlock(KeyPair keyPair)
        {
            var genesisBlock = _genesisBuilder.Build();
            if (CurrentBlockHeader != null)
                return false;
            PersistBlockManually(genesisBlock.Block, genesisBlock.Transactions);
            return true;
        }
        
        public void PersistBlockManually(Block block, IEnumerable<SignedTransaction> transactions)
        {
            foreach (var tx in transactions)
            {
                var result = _transactionManager.Persist(tx);
                if (result == OperatingError.Ok)
                    continue;
                throw new InvalidTransactionException(result);
            }
            var error = _blockManager.Persist(block);
            if (error != OperatingError.Ok)
                throw new InvalidBlockException(error);
        }
    }
}