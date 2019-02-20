using System.Collections.Generic;
using Phorkus.Core.Blockchain.Genesis;
using Phorkus.Core.Blockchain.OperationManager;
using Phorkus.Core.Config;
using Phorkus.Proto;
using Phorkus.Storage.State;
using Phorkus.Utility;
using Phorkus.Utility.Utils;

namespace Phorkus.Core.Blockchain
{
    public class BlockchainManager : IBlockchainManager, IBlockchainContext
    {
        private readonly IGenesisBuilder _genesisBuilder;
        private readonly IBlockManager _blockManager;
        private readonly IConfigManager _configManager;
        private readonly IStateManager _stateManager;

        public ulong CurrentBlockHeight => _stateManager.LastApprovedSnapshot.Blocks.GetTotalBlockHeight();
        public Block CurrentBlock => _stateManager.LastApprovedSnapshot.Blocks.GetBlockByHeight(CurrentBlockHeight);

        public BlockchainManager(
            IGenesisBuilder genesisBuilder,
            IBlockManager blockManager,
            IConfigManager configManager,
            IStateManager stateManager)
        {
            _genesisBuilder = genesisBuilder;
            _blockManager = blockManager;
            _configManager = configManager;
            _stateManager = stateManager;
        }
        
        public bool TryBuildGenesisBlock()
        {
            var genesisBlock = _genesisBuilder.Build();
            if (_stateManager.LastApprovedSnapshot.Blocks.GetBlockByHeight(0) != null)
                return false;
            var snapshot = _stateManager.NewSnapshot();
            var genesisConfig = _configManager.GetConfig<GenesisConfig>("genesis");
            foreach (var entry in genesisConfig.Balances)
                snapshot.Balances.SetBalance(entry.Key.HexToBytes().ToUInt160(), Money.Parse(entry.Value));
            _stateManager.Approve();
            var error = _blockManager.Execute(genesisBlock.Block, genesisBlock.Transactions, commit: false, checkStateHash: false);
            if (error != OperatingError.Ok)
                throw new InvalidBlockException(error);
            _stateManager.Commit();
            return true;
        }
        
        public void PersistBlockManually(Block block, IEnumerable<TransactionReceipt> transactions)
        {
            var error = _blockManager.Execute(block, transactions, commit: true, checkStateHash: false);
            if (error != OperatingError.Ok)
                throw new InvalidBlockException(error);
        }
    }
}