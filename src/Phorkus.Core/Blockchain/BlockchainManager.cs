using System;
using System.Collections.Generic;
using System.Linq;
using Phorkus.Core.Blockchain.Genesis;
using Phorkus.Core.Blockchain.OperationManager;
using Phorkus.Core.Config;
using Phorkus.Core.Utils;
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

        public UInt256 CalcStateHash(Block block, IEnumerable<TransactionReceipt> transactionReceipts)
        {
            var(operatingError, removeTransactions, stateHash, relayTransactions) = _blockManager.Emulate(block, transactionReceipts);
            if (operatingError != OperatingError.Ok)
                throw new InvalidBlockException(operatingError);
            if (removeTransactions.Count > 0)
                throw new InvalidBlockException(OperatingError.InvalidTransaction);
            if (relayTransactions.Count > 0)
                throw new InvalidBlockException(OperatingError.BlockGasOverflow);
            return stateHash;
        }

        public void PersistBlockManually(Block block, IEnumerable<TransactionReceipt> transactions)
        {
            var error = _blockManager.Execute(block, transactions, commit: true, checkStateHash: false);
            if (error != OperatingError.Ok)
                throw new InvalidBlockException(error);
        }
    }
}