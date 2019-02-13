using System.Collections.Generic;
using Phorkus.Core.Blockchain.Genesis;
using Phorkus.Core.Blockchain.OperationManager;
using Phorkus.Core.Blockchain.OperationManager.BlockManager;
using Phorkus.Proto;
using Phorkus.Storage.State;

namespace Phorkus.Core.Blockchain
{
    public class BlockchainManager : IBlockchainManager, IBlockchainContext
    {
        private readonly IGenesisBuilder _genesisBuilder;
        private readonly IBlockManager _blockManager;
        private readonly IStateManager _stateManager;

        public ulong CurrentBlockHeight => _stateManager.LastApprovedSnapshot.Blocks.GetTotalBlockHeight();
        public Block CurrentBlock => _stateManager.LastApprovedSnapshot.Blocks.GetBlockByHeight(CurrentBlockHeight);

        public BlockchainManager(
            IGenesisBuilder genesisBuilder,
            IBlockManager blockManager,
            IStateManager stateManager)
        {
            _genesisBuilder = genesisBuilder;
            _blockManager = blockManager;
            _stateManager = stateManager;
        }

        public bool TryBuildGenesisBlock()
        {
            var genesisBlock = _genesisBuilder.Build();
            if (_stateManager.LastApprovedSnapshot.Blocks.GetBlockByHeight(0) != null)
                return false;
            PersistBlockManually(genesisBlock.Block, genesisBlock.Transactions);
            return true;
        }

        public void PersistBlockManually(Block block, IEnumerable<AcceptedTransaction> transactions)
        {
            var error = _blockManager.Execute(block, transactions, commit: true, checkStateHash: false);
            if (error != OperatingError.Ok)
                throw new InvalidBlockException(error);
        }
    }
}